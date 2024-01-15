using System;
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
    public class Ecma2Export
    {
        private readonly ILogger logger;
        private readonly IEcma2ConfigParameters configParameters;
        private readonly IServiceProvider serviceProvider;

        private List<IObjectExportProvider> providerCache;
        private ExportContext exportContext;

        public Ecma2Export(Ecma2Initializer init)
        {
            this.serviceProvider = init.Build();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<Ecma2Export>>();
            this.configParameters = this.serviceProvider.GetRequiredService<IEcma2ConfigParameters>();
        }

        private List<IObjectExportProvider> Providers
        {
            get
            {
                if (this.providerCache == null)
                {
                    this.providerCache = this.serviceProvider.GetServices<IObjectExportProvider>().ToList();
                }

                return this.providerCache;
            }
        }

        public Task CloseExportConnectionAsync(CloseExportConnectionRunStep exportRunStep)
        {
            this.logger.LogInformation($"Closing export connection: {exportRunStep.Reason}");

            if (this.exportContext == null)
            {
                this.logger.LogTrace("No export context detected");
                return Task.CompletedTask;
            }

            this.exportContext.Timer.Stop();

            if (exportRunStep.Reason != CloseReason.Normal)
            {
                if (this.exportContext.CancellationTokenSource != null)
                {
                    this.logger.LogInformation("Cancellation request received");
                    this.exportContext.CancellationTokenSource.Cancel();
                    this.exportContext.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    this.logger.LogInformation("Cancellation completed");
                }
            }

            this.logger.LogInformation("Export operation complete");
            this.logger.LogInformation($"Exported {this.exportContext.ExportedItemCount} objects");
            this.logger.LogInformation($"Export duration: {this.exportContext.Timer.Elapsed}");

            if (this.exportContext.ExportedItemCount > 0 && this.exportContext.Timer.Elapsed.TotalSeconds > 0)
            {
                this.logger.LogInformation($"Speed: {(this.exportContext.ExportedItemCount / this.exportContext.Timer.Elapsed.TotalSeconds):N2} obj/sec");
                this.logger.LogInformation($"Average: {(this.exportContext.Timer.Elapsed.TotalSeconds / this.exportContext.ExportedItemCount):N2} sec/obj");
            }

            return Task.CompletedTask;
        }

        public async Task OpenExportConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            this.configParameters.SetConfigParameters(configParameters);

            this.exportContext = new ExportContext()
            {
                ConfigParameters = configParameters
            };

            try
            {
                this.logger.LogInformation("Starting export");

                var initializers = this.serviceProvider.GetServices<IOperationInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        this.logger.LogInformation("Launching initializer");
                        await initializer.InitializeExportAsync(this.exportContext);
                        this.logger.LogInformation("Initializer complete");
                    }
                }

                await this.InitializeProvidersAsync(this.exportContext);
                this.exportContext.Timer.Start();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Failed to open export connection");
                throw;
            }
        }

        public Task<PutExportEntriesResults> PutExportEntriesAsync(IList<CSEntryChange> csentries)
        {
            PutExportEntriesResults results = new PutExportEntriesResults();

            ParallelOptions po = new ParallelOptions
            {
                MaxDegreeOfParallelism = GlobalSettings.ExportThreadCount,
                CancellationToken = this.exportContext.Token
            };

            Parallel.ForEach(csentries, po, (csentry) =>
            {
                Stopwatch timer = new Stopwatch();

                int number = Interlocked.Increment(ref this.exportContext.ExportedItemCount);
                string record = $"{number}:{csentry.ObjectModificationType}:{csentry.ObjectType}:{csentry.DN}";
                CSEntryChangeResult result = null;

                this.logger.LogInformation($"Exporting record {record}");

                try
                {
                    timer.Start();
                    result = AsyncHelper.RunSync(this.PutCSEntryChangeAsync(csentry));
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, $"An error occurred exporting record {record}");
                    result = CSEntryChangeResult.Create(csentry.Identifier, null, MAExportError.ExportErrorCustomContinueRun, ex.Message, ex.ToString());
                }
                finally
                {
                    timer.Stop();

                    if (result == null)
                    {
                        this.logger.LogError($"CSEntryResult for object {record} was null");
                    }
                    else
                    {
                        lock (results)
                        {
                            results.CSEntryChangeResults.Add(result);
                        }
                    }

                    this.logger.LogTrace($"Export of record {record} returned '{result?.ErrorCode.ToString().ToLower() ?? "<null>"}' and took {timer.Elapsed}");
                }
            });

            this.logger.LogInformation($"Page complete. Export count: {this.exportContext.ExportedItemCount}");
            return Task.FromResult(results);
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