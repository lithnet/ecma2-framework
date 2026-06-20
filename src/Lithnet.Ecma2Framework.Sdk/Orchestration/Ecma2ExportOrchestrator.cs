using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.MetadirectoryServices;
using Lithnet.Ecma2Framework.Internal;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Orchestrates a provider-based export operation.  One orchestrator instance is created
    /// per export run and is driven by the worker host via <see cref="OpenAsync"/>,
    /// repeated <see cref="PutAsync"/> calls, and a final <see cref="CloseAsync"/>.
    /// </summary>
    /// <remarks>
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenAsync"/> throws if no <see cref="IObjectExportProvider"/> is
    ///     registered, or if provider initialisation throws a non-<see cref="NotImplementedException"/>.</item>
    ///   <item><see cref="PutAsync"/> routes each entry individually.  When a provider throws
    ///     an exception for an entry, that entry is converted to an
    ///     <see cref="MAExportError.ExportErrorCustomContinueRun"/> result carrying the
    ///     exception message and stack trace; the rest of the batch continues unaffected.
    ///     A <see cref="CSEntryChangeResult"/> returned with a non-Success error code by the
    ///     provider is collected as-is without wrapping — per-entry error handling is the
    ///     provider's responsibility.</item>
    ///   <item>When no provider claims an entry (i.e. no provider returns true from
    ///     <see cref="IObjectExportProvider.CanExportAsync"/>), the entry receives an
    ///     <see cref="MAExportError.ExportErrorCustomContinueRun"/> result.</item>
    ///   <item><see cref="CloseAsync"/> stops the internal timer and logs summary statistics;
    ///     it does not throw.</item>
    /// </list>
    /// </remarks>
    internal sealed class Ecma2ExportOrchestrator
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<Ecma2ExportOrchestrator> logger;

        private ExportContext context;

        // Lazily-resolved and cached list of export providers for the lifetime of this run.
        private List<IObjectExportProvider> providerCache;

        /// <summary>
        /// Initialises a new <see cref="Ecma2ExportOrchestrator"/> using the supplied service provider.
        /// All <see cref="IObjectExportProvider"/> instances are resolved from the container when
        /// <see cref="OpenAsync"/> is called.
        /// </summary>
        /// <param name="serviceProvider">The DI container built for this export run.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
        public Ecma2ExportOrchestrator(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetService<ILogger<Ecma2ExportOrchestrator>>();
        }

        /// <summary>
        /// Initialises all registered <see cref="IObjectExportProvider"/> instances and starts
        /// the internal timer.  Must be called exactly once before <see cref="PutAsync"/>.
        /// </summary>
        /// <param name="context">The export context for this run.</param>
        /// <returns>A task that completes when all providers have been initialised.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        public async Task OpenAsync(ExportContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            this.context = context;

            this.LogInformation("Starting export");

            this.providerCache = new List<IObjectExportProvider>(
                this.serviceProvider.GetServices<IObjectExportProvider>());

            foreach (IObjectExportProvider provider in this.providerCache)
            {
                try
                {
                    await provider.InitializeAsync(this.context);
                }
                catch (NotImplementedException) { }
            }

            this.context.Timer.Start();

            this.LogInformation("Export providers initialised ({0} registered)", this.providerCache.Count);
        }

        /// <summary>
        /// Processes a batch of <see cref="CSEntryChange"/> objects by routing each entry to
        /// the first <see cref="IObjectExportProvider"/> that claims it via
        /// <see cref="IObjectExportProvider.CanExportAsync"/>.
        /// </summary>
        /// <param name="entries">The batch of entries to export.  Must not be null.</param>
        /// <param name="ct">A cancellation token.</param>
        /// <returns>
        /// A list containing exactly one <see cref="CSEntryChangeResult"/> per input entry,
        /// in the same order as <paramref name="entries"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="entries"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called before <see cref="OpenAsync"/>.</exception>
        public async Task<IList<CSEntryChangeResult>> PutAsync(IList<CSEntryChange> entries, CancellationToken ct)
        {
            if (entries == null)
            {
                throw new ArgumentNullException("entries");
            }

            if (this.context == null)
            {
                throw new InvalidOperationException("PutAsync was called before OpenAsync.");
            }

            // Export is per-object; process the batch with bounded concurrency (ExportThreads). The worker
            // is async-native, so each entry is awaited directly — no sync-over-async marshalling. Results
            // are keyed by Identifier (not position), so the order they are added does not matter.
            Ecma2FrameworkOptions options = this.serviceProvider.GetRequiredService<IOptions<Ecma2FrameworkOptions>>().Value;

            List<CSEntryChangeResult> results = new List<CSEntryChangeResult>(entries.Count);

            await ParallelForEach.ForEachAsync(entries, Math.Max(1, options.ExportThreads), ct, async (csentry, token) =>
            {
                int number = Interlocked.Increment(ref this.context.ExportedItemCount);
                string record = string.Format(
                    "{0}:{1}:{2}:{3}",
                    number,
                    csentry.ObjectModificationType,
                    csentry.ObjectType,
                    csentry.DN);

                this.LogInformation("Exporting record {0}", record);

                Stopwatch timer = Stopwatch.StartNew();
                CSEntryChangeResult result = null;

                try
                {
                    result = await this.PutCSEntryChangeAsync(csentry, token);
                }
                catch (Exception ex)
                {
                    this.LogError(ex, "An error occurred exporting record {0}", record);

                    result = CSEntryChangeResult.Create(
                        csentry.Identifier,
                        new List<AttributeChange>(),
                        MAExportError.ExportErrorCustomContinueRun,
                        ex.Message,
                        ex.ToString());
                }
                finally
                {
                    timer.Stop();

                    if (result == null)
                    {
                        this.LogError(null, "CSEntryResult for record {0} was null", record);
                    }
                    else
                    {
                        lock (results)
                        {
                            results.Add(result);
                        }
                    }

                    this.LogInformation(
                        "Export of record {0} returned '{1}' in {2}",
                        record,
                        result != null ? result.ErrorCode.ToString() : "<null>",
                        timer.Elapsed);
                }
            });

            this.LogInformation("Batch complete. Export count: {0}", this.context.ExportedItemCount);

            return results;
        }

        /// <summary>
        /// Stops the internal timer and logs export statistics.
        /// </summary>
        /// <returns>A completed task.</returns>
        public Task CloseAsync()
        {
            if (this.context == null)
            {
                return Task.CompletedTask;
            }

            this.context.Timer.Stop();

            this.LogInformation("Export operation complete");
            this.LogInformation("Exported {0} objects", this.context.ExportedItemCount);
            this.LogInformation("Export duration: {0}", this.context.Timer.Elapsed);

            if (this.context.ExportedItemCount > 0 && this.context.Timer.Elapsed.TotalSeconds > 0)
            {
                this.LogInformation(
                    "Speed: {0:N2} obj/sec",
                    this.context.ExportedItemCount / this.context.Timer.Elapsed.TotalSeconds);

                this.LogInformation(
                    "Average: {0:N2} sec/obj",
                    this.context.Timer.Elapsed.TotalSeconds / this.context.ExportedItemCount);
            }

            return Task.CompletedTask;
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private async Task<CSEntryChangeResult> PutCSEntryChangeAsync(CSEntryChange csentry, CancellationToken ct)
        {
            IObjectExportProvider provider = await this.FindProviderForEntryAsync(csentry);
            return await provider.PutCSEntryChangeAsync(csentry, ct);
        }

        private async Task<IObjectExportProvider> FindProviderForEntryAsync(CSEntryChange csentry)
        {
            foreach (IObjectExportProvider provider in this.providerCache)
            {
                try
                {
                    if (await provider.CanExportAsync(csentry))
                    {
                        return provider;
                    }
                }
                catch (NotImplementedException) { }
            }

            throw new InvalidOperationException(
                string.Format(
                    "An export provider for the type '{0}' could not be found.",
                    csentry.ObjectType));
        }

        // -------------------------------------------------------------------------
        // Logging helpers (guard against null logger)
        // -------------------------------------------------------------------------

        private void LogInformation(string message, params object[] args)
        {
            if (this.logger != null)
            {
                this.logger.LogInformation(message, args);
            }
        }

        private void LogError(Exception ex, string message, params object[] args)
        {
            if (this.logger != null)
            {
                this.logger.LogError(ex, message, args);
            }
        }
    }
}
