using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
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
    /// Orchestrates a provider-based import operation using the producer/consumer pattern.
    /// One orchestrator instance is created per import run and is driven by the worker host
    /// via <see cref="OpenAsync"/>, repeated <see cref="GetNextPageAsync"/> calls, and a
    /// final <see cref="CloseAsync"/>.
    /// </summary>
    /// <remarks>
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenAsync"/> throws if no provider can be found for a schema type,
    ///     or if provider initialisation throws a non-<see cref="NotImplementedException"/>.</item>
    ///   <item><see cref="GetNextPageAsync"/> rethrows the producer exception if the background
    ///     Task has faulted, so callers observe the failure rather than receiving a silent empty
    ///     page after the collection is completed by an exception.</item>
    ///   <item><see cref="CloseAsync"/> awaits the producer to completion and serialises the
    ///     outbound watermark map to JSON.  If no watermark entries were collected it returns
    ///     an empty string.</item>
    /// </list>
    /// </remarks>
    internal sealed class Ecma2ImportOrchestrator
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<Ecma2ImportOrchestrator> logger;
        private ImportContext context;

        /// <summary>
        /// Initialises a new <see cref="Ecma2ImportOrchestrator"/> using the supplied service provider.
        /// All <see cref="IObjectImportProvider"/> instances are resolved from the container when
        /// <see cref="OpenAsync"/> is called.
        /// </summary>
        /// <param name="serviceProvider">The DI container built for this import run.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
        public Ecma2ImportOrchestrator(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetService<ILogger<Ecma2ImportOrchestrator>>();
        }

        /// <summary>
        /// Starts the background producer that calls <see cref="IObjectImportProvider.GetCSEntryChangesAsync"/>
        /// for each type in the schema.  Returns after launching the producer Task; the caller
        /// should then call <see cref="GetNextPageAsync"/> to consume entries.
        /// </summary>
        /// <param name="context">The import context for this run.</param>
        /// <returns>A task that completes when the producer has been launched (not when it finishes).</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        public Task OpenAsync(ImportContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            this.context = context;

            this.LogInformation("Starting {0} import", context.InDelta ? "delta" : "full");

            context.Timer.Start();

            context.Producer = Task.Run(async () => await this.RunProducerAsync());

            this.LogInformation("Import producer launched");

            return Task.CompletedTask;
        }

        /// <summary>
        /// Drains up to <paramref name="pageSize"/> entries from the blocking collection and
        /// returns them as an <see cref="ImportPage"/>.  Blocks until entries are available or
        /// the collection is completed.
        /// </summary>
        /// <param name="pageSize">Maximum number of entries to include in the page.  Must be positive.</param>
        /// <param name="ct">A cancellation token that can abort the drain.</param>
        /// <returns>
        /// An <see cref="ImportPage"/> whose <c>MoreToImport</c> is true when the page was
        /// filled to capacity and more entries may remain.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Rethrown (wrapping the original) when the producer Task has faulted.
        /// </exception>
        public Task<ImportPage> GetNextPageAsync(int pageSize, CancellationToken ct)
        {
            if (this.context == null)
            {
                throw new InvalidOperationException("GetNextPageAsync was called before OpenAsync.");
            }

            // If the producer faulted, surface its exception immediately.
            if (this.context.Producer != null && this.context.Producer.IsFaulted)
            {
                throw new InvalidOperationException(
                    "The import producer encountered an exception.",
                    this.context.Producer.Exception);
            }

            List<CSEntryChange> entries = new List<CSEntryChange>();

            if (this.context.ImportItems.IsCompleted)
            {
                return Task.FromResult(new ImportPage(entries, false));
            }

            int count = 0;
            bool mayHaveMore = false;

            while (!this.context.ImportItems.IsCompleted)
            {
                CSEntryChange csentry = null;

                try
                {
                    csentry = this.context.ImportItems.Take(ct);
                }
                catch (InvalidOperationException)
                {
                    // Collection was completed between the IsCompleted check and the Take call.
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (csentry != null)
                {
                    entries.Add(csentry);
                    Interlocked.Increment(ref this.context.ImportedItemCount);
                    count++;
                }

                if (count >= pageSize)
                {
                    mayHaveMore = true;
                    break;
                }
            }

            return Task.FromResult(new ImportPage(entries, mayHaveMore));
        }

        /// <summary>
        /// Waits for the producer to finish (or faults), then serialises and returns the outbound
        /// watermark as a JSON string.  Returns an empty string if no watermarks were collected.
        /// </summary>
        /// <returns>
        /// A JSON-serialised dictionary of outbound watermarks keyed by type name,
        /// or an empty string if there are none.
        /// </returns>
        public async Task<string> CloseAsync()
        {
            if (this.context == null)
            {
                return string.Empty;
            }

            if (this.context.Producer != null)
            {
                try
                {
                    await this.context.Producer;
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is expected when the import is aborted; not an error.
                }
                catch (Exception ex)
                {
                    this.LogError(ex, "Producer faulted during CloseAsync");
                    throw;
                }
            }

            this.context.Timer.Stop();

            this.LogInformation("Import operation complete. Imported {0} objects.", this.context.ImportedItemCount);

            if (this.context.OutgoingWatermark != null && this.context.OutgoingWatermark.Count > 0)
            {
                string watermarkJson = JsonSerializer.Serialize(this.context.OutgoingWatermark, JsonOptions);
                this.LogTrace("Outbound watermark: {0}", watermarkJson);
                return watermarkJson;
            }

            return string.Empty;
        }

        // -------------------------------------------------------------------------
        // Private producer logic
        // -------------------------------------------------------------------------

        private async Task RunProducerAsync()
        {
            this.LogInformation("Producer thread starting");

            try
            {
                // Imports are per-schema-type; bound how many types produce concurrently by ImportThreads.
                // The buffer (ImportItems) is intentionally unbounded — the producer fills it as fast as it can.
                Ecma2FrameworkOptions options = this.serviceProvider.GetRequiredService<IOptions<Ecma2FrameworkOptions>>().Value;

                await ParallelForEach.ForEachAsync(
                    this.context.Types.Types,
                    Math.Max(1, options.ImportThreads),
                    this.context.CancellationTokenSource.Token,
                    async (type, ct) => await this.ProduceForTypeAsync(type));
            }
            catch (OperationCanceledException)
            {
                this.LogInformation("Producer cancelled");
            }
            catch (Exception ex)
            {
                this.LogError(ex, "Producer thread encountered an exception");
                throw;
            }
            finally
            {
                this.context.ProducerDuration = this.context.Timer.Elapsed;
                this.LogInformation("Producer complete");
                this.context.ImportItems.CompleteAdding();
            }
        }

        private async Task ProduceForTypeAsync(SchemaType type)
        {
            IObjectImportProvider provider = await this.FindProviderForTypeAsync(type);

            this.LogInformation("Starting import of type {0}", type.Name);

            try
            {
                await provider.InitializeAsync(this.context);
            }
            catch (NotImplementedException) { }

            string inboundWatermark = null;
            this.context.IncomingWatermark?.TryGetValue(type.Name, out inboundWatermark);

            if (this.context.InDelta && inboundWatermark == null)
            {
                throw new InvalidOperationException(
                    string.Format(
                        "No watermark was available for the type '{0}'. Please run a full import first.",
                        type.Name));
            }

            await provider.GetCSEntryChangesAsync(
                type,
                this.context.ImportCollectionWrapper,
                inboundWatermark,
                this.context.CancellationTokenSource.Token);

            try
            {
                string outboundWatermark = await provider.GetOutboundWatermark(
                    type,
                    this.context.CancellationTokenSource.Token);

                if (outboundWatermark != null)
                {
                    this.context.OutgoingWatermark.TryAdd(type.Name, outboundWatermark);
                }
            }
            catch (NotImplementedException) { }

            this.LogInformation("Import of type {0} complete", type.Name);
        }

        private async Task<IObjectImportProvider> FindProviderForTypeAsync(SchemaType type)
        {
            foreach (IObjectImportProvider provider in this.serviceProvider.GetServices<IObjectImportProvider>())
            {
                try
                {
                    if (await provider.CanImportAsync(type))
                    {
                        return provider;
                    }
                }
                catch (NotImplementedException) { }
            }

            throw new InvalidOperationException(
                string.Format(
                    "An import provider for the type '{0}' could not be found.",
                    type.Name));
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

        private void LogTrace(string message, params object[] args)
        {
            if (this.logger != null)
            {
                this.logger.LogTrace(message, args);
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
