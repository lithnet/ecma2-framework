using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;
using Newtonsoft.Json;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Import
    {
        private readonly ILogger logger;
        private readonly IEcma2ConfigParameters configParameters;

        public static bool AttachDebuggerOnLaunch { get; set; } = true;

        private ImportContext importContext;

        private readonly IServiceProvider serviceProvider;

        public Ecma2Import(Ecma2Initializer init)
        {
            this.serviceProvider = init.Build();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<Ecma2Import>>();
            this.configParameters = this.serviceProvider.GetRequiredService<IEcma2ConfigParameters>();
        }

        private int Batch { get; set; }

        public async Task<OpenImportConnectionResults> OpenImportConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            this.configParameters.SetConfigParameters(configParameters);

            this.importContext = new ImportContext()
            {
                RunStep = importRunStep,
                ImportItems = new BlockingCollection<CSEntryChange>(),
                ConfigParameters = configParameters,
                Types = types
            };

            try
            {
                this.logger.LogInformation("Starting {0} import", this.importContext.InDelta ? "delta" : "full");

                if (!string.IsNullOrEmpty(importRunStep.CustomData))
                {
                    try
                    {
                        this.importContext.IncomingWatermark = JsonConvert.DeserializeObject<WatermarkKeyedCollection>(importRunStep.CustomData);
                    }
                    catch (Exception ex)
                    {
                        this.logger.LogError(ex, "Could not deserialize watermark");
                    }
                }

                var initializers = this.serviceProvider.GetServices<IOperationInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        this.logger.LogInformation("Launching initializer");
                        await initializer.InitializeImportAsync(this.importContext);
                        this.logger.LogInformation("Initializer complete");
                    }
                }

                this.importContext.Timer.Start();

                this.importContext.Producer = this.StartCreatingCSEntryChangesAsync();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Error opening import connection");
                throw;
            }

            return new OpenImportConnectionResults();
        }

        public Task<GetImportEntriesResults> GetImportEntriesPageAsync()
        {
            int count = 0;
            bool mayHaveMore = false;
            GetImportEntriesResults results = new GetImportEntriesResults { CSEntries = new List<CSEntryChange>() };

            if (this.importContext.ImportItems.IsCompleted)
            {
                results.MoreToImport = false;
                return Task.FromResult(results);
            }

            this.Batch++;
            this.logger.LogTrace($"Producing page {this.Batch}");

            while (!this.importContext.ImportItems.IsCompleted || this.importContext.CancellationTokenSource.IsCancellationRequested)
            {
                count++;
                CSEntryChange csentry = null;

                try
                {
                    csentry = this.importContext.ImportItems.Take();
                    this.importContext.ImportedItemCount++;

                    this.logger.LogTrace($"Got record {this.importContext.ImportedItemCount}:{csentry.ErrorCodeImport}:{csentry?.ObjectModificationType}:{csentry?.ObjectType}:{csentry?.DN}");
                }
                catch (InvalidOperationException)
                {
                }

                if (csentry != null)
                {
                    results.CSEntries.Add(csentry);
                }

                if (count >= this.importContext.RunStep.PageSize)
                {
                    mayHaveMore = true;
                    break;
                }
            }

            if (this.importContext.Producer?.IsFaulted == true)
            {
                throw new ExtensibleExtensionException("The producer thread encountered an exception", this.importContext.Producer.Exception);
            }

            if (mayHaveMore)
            {
                this.logger.LogTrace($"Page {this.Batch} complete");
            }
            else
            {
                this.logger.LogInformation("CSEntryChange consumption complete");
                this.Batch = 0;
            }

            results.MoreToImport = mayHaveMore;
            return Task.FromResult(results);
        }

        public Task<CloseImportConnectionResults> CloseImportConnectionAsync(CloseImportConnectionRunStep importRunStep)
        {
            this.logger.LogInformation("Closing import connection: {0}", importRunStep.Reason);

            if (this.importContext == null)
            {
                this.logger.LogTrace("No import context detected");
                return Task.FromResult(new CloseImportConnectionResults());
            }

            this.importContext.Timer.Stop();

            if (importRunStep.Reason != CloseReason.Normal)
            {
                if (this.importContext.CancellationTokenSource != null)
                {
                    this.logger.LogInformation("Cancellation request received");
                    this.importContext.CancellationTokenSource.Cancel();
                    this.importContext.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    this.logger.LogInformation("Cancellation completed");
                }
            }

            this.logger.LogInformation("Import operation complete");
            this.logger.LogInformation($"Imported {this.importContext.ImportedItemCount} objects");

            if (this.importContext.ImportedItemCount > 0 && this.importContext.Timer.Elapsed.TotalSeconds > 0)
            {
                if (this.importContext.ProducerDuration.TotalSeconds > 0)
                {
                    this.logger.LogInformation($"CSEntryChange production duration: {this.importContext.ProducerDuration}");
                    this.logger.LogInformation($"CSEntryChange production speed: {(this.importContext.ImportedItemCount / this.importContext.ProducerDuration.TotalSeconds):N2} obj/sec");
                }

                this.logger.LogInformation($"Import duration: {this.importContext.Timer.Elapsed}");
                this.logger.LogInformation($"Import speed: {(this.importContext.ImportedItemCount / this.importContext.Timer.Elapsed.TotalSeconds):N2} obj/sec");
            }

            if (this.importContext.OutgoingWatermark?.Any() == true)
            {
                string wm = JsonConvert.SerializeObject(this.importContext.OutgoingWatermark);
                this.logger.LogTrace($"Watermark: {wm}");
                return Task.FromResult(new CloseImportConnectionResults(wm));
            }
            else
            {
                return Task.FromResult(new CloseImportConnectionResults());
            }
        }

        private Task StartCreatingCSEntryChangesAsync()
        {
            this.logger.LogInformation("Starting producer thread");

            try
            {
                List<Task> taskList = new List<Task>();

                foreach (SchemaType type in this.importContext.Types.Types)
                {
                    taskList.Add(this.CreateCSEntryChangesForTypeAsync(type));
                }

                Task.WaitAll(taskList.ToArray(), this.importContext.CancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                this.logger.LogInformation("Producer thread cancelled");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Producer thread encountered an exception");
                throw;
            }
            finally
            {
                this.importContext.ProducerDuration = this.importContext.Timer.Elapsed;
                this.logger.LogInformation("CSEntryChange production complete");
                this.importContext.ImportItems.CompleteAdding();
            }

            return Task.CompletedTask;
        }

        private async Task CreateCSEntryChangesForTypeAsync(SchemaType type)
        {
            IObjectImportProvider provider = await this.GetProviderForTypeAsync(type);
            this.logger.LogInformation($"Starting import of type {type.Name}");
            await provider.InitializeAsync(this.importContext);
            await provider.GetCSEntryChangesAsync(type);
            this.logger.LogInformation($"Import of type {type.Name} completed");
        }

        private async Task<IObjectImportProvider> GetProviderForTypeAsync(SchemaType type)
        {
            foreach (IObjectImportProvider provider in this.serviceProvider.GetServices<IObjectImportProvider>())
            {
                if (await provider.CanImportAsync(type))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException($"An import provider for the type '{type.Name}' could not be found");
        }
    }
}