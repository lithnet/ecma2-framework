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

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// <para>Provides the main import functionality for the ECMA2 framework</para>
    /// <para>This class is called by generated code, and should not be called directly</para>
    /// </summary>
    public class Ecma2Import : Ecma2Base
    {
        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        private ImportContext context;

        public Ecma2Import(Ecma2Initializer initializer) : base(initializer)
        {
        }

        private int Batch { get; set; }

        public async Task<OpenImportConnectionResults> OpenImportConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            this.InitializeDIContainer(configParameters);

            this.context = new ImportContext(importRunStep, types);

            try
            {
                this.Logger.LogInformation("Starting {0} import", this.context.InDelta ? "delta" : "full");

                if (!string.IsNullOrEmpty(importRunStep.CustomData))
                {
                    try
                    {
                        this.context.IncomingWatermark = JsonSerializer.Deserialize<ConcurrentDictionary<string, string>>(importRunStep.CustomData, jsonOptions);
                    }
                    catch (Exception ex)
                    {
                        this.Logger.LogWarning(ex, "Could not deserialize watermark");
                        this.context.IncomingWatermark = new ConcurrentDictionary<string, string>();
                    }
                }

                var initializer = this.ServiceProvider.GetService<IContextInitializer>();

                if (initializer != null)
                {
                    this.Logger.LogInformation("Launching initializer");

                    try
                    {
                        await initializer.InitializeImportAsync(this.context);
                    }
                    catch (NotImplementedException) { }

                    this.Logger.LogInformation("Initializer complete");
                }

                this.context.Timer.Start();

                this.context.Producer = Task.Run(async () => await this.StartCreatingCSEntryChangesAsync());

                this.Logger.LogInformation("Import initialization complete");
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

            if (this.context.ImportItems.IsCompleted)
            {
                results.MoreToImport = false;
                return Task.FromResult(results);
            }

            this.Batch++;
            this.Logger.LogTrace($"Consuming page {this.Batch}");

            while (!this.context.ImportItems.IsCompleted || this.context.CancellationTokenSource.IsCancellationRequested)
            {
                count++;
                CSEntryChange csentry = null;

                try
                {
                    csentry = this.context.ImportItems.Take();
                    this.context.ImportedItemCount++;

                    this.Logger.LogTrace($"Got record {this.context.ImportedItemCount}:{csentry.ErrorCodeImport}:{csentry?.ObjectModificationType}:{csentry?.ObjectType}:{csentry?.DN}");
                }
                catch (InvalidOperationException)
                {
                }

                if (csentry != null)
                {
                    results.CSEntries.Add(csentry);
                }

                if (count >= this.context.RunStep.PageSize)
                {
                    mayHaveMore = true;
                    break;
                }
            }

            if (this.context.Producer?.IsFaulted == true)
            {
                throw new ExtensibleExtensionException("The producer thread encountered an exception", this.context.Producer.Exception);
            }

            if (mayHaveMore)
            {
                this.Logger.LogTrace($"Consuming page {this.Batch} complete");
            }
            else
            {
                this.Logger.LogInformation("Consumption is complete");
                this.Batch = 0;
            }

            results.MoreToImport = mayHaveMore;
            return Task.FromResult(results);
        }

        public Task<CloseImportConnectionResults> CloseImportConnectionAsync(CloseImportConnectionRunStep importRunStep)
        {
            this.Logger.LogInformation("Closing import connection: {0}", importRunStep.Reason);

            if (this.context == null)
            {
                this.Logger.LogTrace("No import context detected");
                return Task.FromResult(new CloseImportConnectionResults());
            }

            this.context.Timer.Stop();

            if (importRunStep.Reason != CloseReason.Normal)
            {
                if (this.context.CancellationTokenSource != null)
                {
                    this.Logger.LogInformation("Cancellation request received");
                    this.context.CancellationTokenSource.Cancel();
                    this.context.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    this.Logger.LogInformation("Cancellation completed");
                }
            }

            this.Logger.LogInformation("Import operation complete");
            this.Logger.LogInformation($"Imported {this.context.ImportedItemCount} objects");

            if (this.context.ImportedItemCount > 0 && this.context.Timer.Elapsed.TotalSeconds > 0)
            {
                if (this.context.ProducerDuration.TotalSeconds > 0)
                {
                    this.Logger.LogInformation($"CSEntryChange production duration: {this.context.ProducerDuration}");
                    this.Logger.LogInformation($"CSEntryChange production speed: {(this.context.ImportedItemCount / this.context.ProducerDuration.TotalSeconds):N2} obj/sec");
                }

                this.Logger.LogInformation($"Import duration: {this.context.Timer.Elapsed}");
                this.Logger.LogInformation($"Import speed: {(this.context.ImportedItemCount / this.context.Timer.Elapsed.TotalSeconds):N2} obj/sec");
            }

            if (this.context.OutgoingWatermark?.Any() == true)
            {
                string wm = JsonSerializer.Serialize(this.context.OutgoingWatermark, jsonOptions);
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

                foreach (SchemaType type in this.context.Types.Types)
                {
                    taskList.Add(Task.Run(async () => await this.CreateCSEntryChangesForTypeAsync(type)));
                }

                Task.WaitAll(taskList.ToArray(), this.context.CancellationTokenSource.Token);
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
                this.context.ProducerDuration = this.context.Timer.Elapsed;
                this.Logger.LogInformation("Producer thread complete");
                this.context.ImportItems.CompleteAdding();
            }

            return Task.CompletedTask;
        }

        private async Task CreateCSEntryChangesForTypeAsync(SchemaType type)
        {
            IObjectImportProvider provider = await this.GetProviderForTypeAsync(type);
            this.Logger.LogInformation($"Starting import of type {type.Name}");

            try
            {
                await provider.InitializeAsync(this.context);
            }
            catch (NotImplementedException) { }

            this.context.IncomingWatermark.TryGetValue(type.Name, out var inboundWatermark);

            if (this.context.InDelta && inboundWatermark == null)
            {
                throw new WarningNoWatermarkException($"No watermark was available for the type '{type.Name}'. Please run a full import first");
            }

            await provider.GetCSEntryChangesAsync(type, this.context.ImportCollectionWrapper, inboundWatermark, this.context.CancellationTokenSource.Token);

            try
            {
                var outboundWatermark = await provider.GetOutboundWatermark(type, this.context.CancellationTokenSource.Token);
                if (outboundWatermark != null)
                {
                    this.context.OutgoingWatermark.TryAdd(type.Name, outboundWatermark);
                }
            }
            catch (NotImplementedException) { }

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