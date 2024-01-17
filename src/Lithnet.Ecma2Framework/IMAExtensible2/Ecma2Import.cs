using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Import : Ecma2Base
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private ImportContext importContext;

        public Ecma2Import(Ecma2Initializer initializer) : base(initializer)
        {
        }

        private int Batch { get; set; }

        public async Task<OpenImportConnectionResults> OpenImportConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            this.InitializeDIContainer(configParameters);

            this.importContext = new ImportContext()
            {
                RunStep = importRunStep,
                ImportItems = new BlockingCollection<CSEntryChange>(),
                Types = types
            };

            try
            {
                this.Logger.LogInformation("Starting {0} import", this.importContext.InDelta ? "delta" : "full");

                if (!string.IsNullOrEmpty(importRunStep.CustomData))
                {
                    try
                    {
                        this.importContext.IncomingWatermark = JsonSerializer.Deserialize<WatermarkKeyedCollection>(importRunStep.CustomData, jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogError(ex, "Could not deserialize watermark");
                    }
                }

                var initializers = this.ServiceProvider.GetServices<IContextInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        this.Logger.LogInformation("Launching initializer");
                        try
                        {
                            await initializer.InitializeImportAsync(this.importContext);
                        }
                        catch (NotImplementedException) { }
                        this.Logger.LogInformation("Initializer complete");
                    }
                }

                this.importContext.Timer.Start();

                this.importContext.Producer = this.StartCreatingCSEntryChangesAsync();
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Error opening import connection");
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
            this.Logger.LogTrace($"Producing page {this.Batch}");

            while (!this.importContext.ImportItems.IsCompleted || this.importContext.CancellationTokenSource.IsCancellationRequested)
            {
                count++;
                CSEntryChange csentry = null;

                try
                {
                    csentry = this.importContext.ImportItems.Take();
                    this.importContext.ImportedItemCount++;

                    this.Logger.LogTrace($"Got record {this.importContext.ImportedItemCount}:{csentry.ErrorCodeImport}:{csentry?.ObjectModificationType}:{csentry?.ObjectType}:{csentry?.DN}");
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
                this.Logger.LogTrace($"Page {this.Batch} complete");
            }
            else
            {
                this.Logger.LogInformation("CSEntryChange consumption complete");
                this.Batch = 0;
            }

            results.MoreToImport = mayHaveMore;
            return Task.FromResult(results);
        }

        public Task<CloseImportConnectionResults> CloseImportConnectionAsync(CloseImportConnectionRunStep importRunStep)
        {
            this.Logger.LogInformation("Closing import connection: {0}", importRunStep.Reason);

            if (this.importContext == null)
            {
                this.Logger.LogTrace("No import context detected");
                return Task.FromResult(new CloseImportConnectionResults());
            }

            this.importContext.Timer.Stop();

            if (importRunStep.Reason != CloseReason.Normal)
            {
                if (this.importContext.CancellationTokenSource != null)
                {
                    this.Logger.LogInformation("Cancellation request received");
                    this.importContext.CancellationTokenSource.Cancel();
                    this.importContext.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    this.Logger.LogInformation("Cancellation completed");
                }
            }

            this.Logger.LogInformation("Import operation complete");
            this.Logger.LogInformation($"Imported {this.importContext.ImportedItemCount} objects");

            if (this.importContext.ImportedItemCount > 0 && this.importContext.Timer.Elapsed.TotalSeconds > 0)
            {
                if (this.importContext.ProducerDuration.TotalSeconds > 0)
                {
                    this.Logger.LogInformation($"CSEntryChange production duration: {this.importContext.ProducerDuration}");
                    this.Logger.LogInformation($"CSEntryChange production speed: {(this.importContext.ImportedItemCount / this.importContext.ProducerDuration.TotalSeconds):N2} obj/sec");
                }

                this.Logger.LogInformation($"Import duration: {this.importContext.Timer.Elapsed}");
                this.Logger.LogInformation($"Import speed: {(this.importContext.ImportedItemCount / this.importContext.Timer.Elapsed.TotalSeconds):N2} obj/sec");
            }

            if (this.importContext.OutgoingWatermark?.Any() == true)
            {
                string wm = JsonSerializer.Serialize(this.importContext.OutgoingWatermark, jsonOptions);
                this.Logger.LogTrace($"Watermark: {wm}");
                return Task.FromResult(new CloseImportConnectionResults(wm));
            }
            else
            {
                return Task.FromResult(new CloseImportConnectionResults());
            }
        }

        private Task StartCreatingCSEntryChangesAsync()
        {
            this.Logger.LogInformation("Starting producer thread");

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
                this.Logger.LogInformation("Producer thread cancelled");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, "Producer thread encountered an exception");
                throw;
            }
            finally
            {
                this.importContext.ProducerDuration = this.importContext.Timer.Elapsed;
                this.Logger.LogInformation("CSEntryChange production complete");
                this.importContext.ImportItems.CompleteAdding();
            }

            return Task.CompletedTask;
        }

        private async Task CreateCSEntryChangesForTypeAsync(SchemaType type)
        {
            IObjectImportProvider provider = await this.GetProviderForTypeAsync(type);
            this.Logger.LogInformation($"Starting import of type {type.Name}");

            try
            {
                await provider.InitializeAsync(this.importContext);
            }
            catch (NotImplementedException) { }

            await provider.GetCSEntryChangesAsync(type);
            this.Logger.LogInformation($"Import of type {type.Name} completed");
        }

        private async Task<IObjectImportProvider> GetProviderForTypeAsync(SchemaType type)
        {
            foreach (IObjectImportProvider provider in this.ServiceProvider.GetServices<IObjectImportProvider>())
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

            throw new InvalidOperationException($"An import provider for the type '{type.Name}' could not be found");
        }
    }
}