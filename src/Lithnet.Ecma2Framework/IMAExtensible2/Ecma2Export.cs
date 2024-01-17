﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Export : Ecma2Base
    {
        private List<IObjectExportProvider> providerCache;
        private ExportContext exportContext;

        public Ecma2Export(Ecma2Initializer initializer) : base(initializer)
        {
        }

        private List<IObjectExportProvider> Providers
        {
            get
            {
                if (this.providerCache == null)
                {
                    this.providerCache = this.ServiceProvider.GetServices<IObjectExportProvider>().ToList();
                }

                return this.providerCache;
            }
        }

        public async Task OpenExportConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            this.InitializeDIContainer(configParameters);

            this.exportContext = new ExportContext();

            try
            {
                this.Logger.LogInformation("Starting export");

                var initializers = this.ServiceProvider.GetServices<IContextInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        this.Logger.LogInformation("Launching initializer");
                        try
                        {
                            await initializer.InitializeExportAsync(this.exportContext);
                        }
                        catch (NotImplementedException) { }

                        this.Logger.LogInformation("Initializer complete");
                    }
                }

                await this.InitializeProvidersAsync(this.exportContext);
                this.exportContext.Timer.Start();
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Failed to open export connection");
                throw;
            }
        }

        public Task<PutExportEntriesResults> PutExportEntriesAsync(IList<CSEntryChange> csEntries)
        {
            PutExportEntriesResults results = new PutExportEntriesResults();

            ParallelOptions po = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, this.exportContext.ExportThreads),
                CancellationToken = this.exportContext.Token
            };

            Parallel.ForEach(csEntries, po, (csentry) =>
            {
                Stopwatch timer = new Stopwatch();

                int number = Interlocked.Increment(ref this.exportContext.ExportedItemCount);
                string record = $"{number}:{csentry.ObjectModificationType}:{csentry.ObjectType}:{csentry.DN}";
                CSEntryChangeResult result = null;

                this.Logger.LogInformation($"Exporting record {record}");

                try
                {
                    timer.Start();
                    result = AsyncHelper.RunSync(this.PutCSEntryChangeAsync(csentry));
                }
                catch (Exception ex)
                {
                    this.Logger.LogError(ex, $"An error occurred exporting record {record}");
                    result = CSEntryChangeResult.Create(csentry.Identifier, null, MAExportError.ExportErrorCustomContinueRun, ex.Message, ex.ToString());
                }
                finally
                {
                    timer.Stop();

                    if (result == null)
                    {
                        this.Logger.LogError($"CSEntryResult for object {record} was null");
                    }
                    else
                    {
                        lock (results)
                        {
                            results.CSEntryChangeResults.Add(result);
                        }
                    }

                    this.Logger.LogTrace($"Export of record {record} returned '{result?.ErrorCode.ToString().ToLower() ?? "<null>"}' and took {timer.Elapsed}");
                }
            });

            this.Logger.LogInformation($"Page complete. Export count: {this.exportContext.ExportedItemCount}");
            return Task.FromResult(results);
        }

        public Task CloseExportConnectionAsync(CloseExportConnectionRunStep exportRunStep)
        {
            this.Logger.LogInformation($"Closing export connection: {exportRunStep.Reason}");

            if (this.exportContext == null)
            {
                this.Logger.LogTrace("No export context detected");
                return Task.CompletedTask;
            }

            this.exportContext.Timer.Stop();

            if (exportRunStep.Reason != CloseReason.Normal)
            {
                if (this.exportContext.CancellationTokenSource != null)
                {
                    this.Logger.LogInformation("Cancellation request received");
                    this.exportContext.CancellationTokenSource.Cancel();
                    this.exportContext.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    this.Logger.LogInformation("Cancellation completed");
                }
            }

            this.Logger.LogInformation("Export operation complete");
            this.Logger.LogInformation($"Exported {this.exportContext.ExportedItemCount} objects");
            this.Logger.LogInformation($"Export duration: {this.exportContext.Timer.Elapsed}");

            if (this.exportContext.ExportedItemCount > 0 && this.exportContext.Timer.Elapsed.TotalSeconds > 0)
            {
                this.Logger.LogInformation($"Speed: {(this.exportContext.ExportedItemCount / this.exportContext.Timer.Elapsed.TotalSeconds):N2} obj/sec");
                this.Logger.LogInformation($"Average: {(this.exportContext.Timer.Elapsed.TotalSeconds / this.exportContext.ExportedItemCount):N2} sec/obj");
            }

            return Task.CompletedTask;
        }

        private async Task<CSEntryChangeResult> PutCSEntryChangeAsync(CSEntryChange csentry)
        {
            IObjectExportProvider provider = await this.GetProviderForTypeAsync(csentry);
            return await provider.PutCSEntryChangeAsync(csentry);
        }

        private async Task<IObjectExportProvider> GetProviderForTypeAsync(CSEntryChange csentry)
        {
            foreach (IObjectExportProvider provider in this.Providers)
            {
                if (await provider.CanExportAsync(csentry))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException($"An export provider for the type '{csentry.ObjectType}' could not be found");
        }

        private async Task InitializeProvidersAsync(ExportContext context)
        {
            foreach (var provider in this.Providers)
            {
                await provider.InitializeAsync(context);
            }
        }
    }
}