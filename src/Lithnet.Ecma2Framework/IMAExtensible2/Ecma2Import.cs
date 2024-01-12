using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;
using Newtonsoft.Json;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Import
    {
        public static bool AttachDebuggerOnLaunch { get; set; } = true;

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private ImportContext importContext;

        private int Batch { get; set; }

        public async Task<OpenImportConnectionResults> OpenImportConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            Logging.SetupLogger(configParameters);

            this.importContext = new ImportContext()
            {
                RunStep = importRunStep,
                ImportItems = new BlockingCollection<CSEntryChange>(),
                ConfigParameters = configParameters,
                Types = types
            };

            try
            {
                logger.Info("Starting {0} import", this.importContext.InDelta ? "delta" : "full");

                this.importContext.ConnectionContext = await InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContextAsync(configParameters, ConnectionContextOperationType.Import);

                if (!string.IsNullOrEmpty(importRunStep.CustomData))
                {
                    try
                    {
                        this.importContext.IncomingWatermark = JsonConvert.DeserializeObject<WatermarkKeyedCollection>(importRunStep.CustomData);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, "Could not deserialize watermark");
                    }
                }

                var initializers = InterfaceManager.GetInstancesOfType<IConnectionInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        logger.Info("Launching initializer");
                        await initializer.InitializeImportAsync(this.importContext);
                        logger.Info("Initializer complete");
                    }
                }

                this.importContext.Timer.Start();

                this.importContext.Producer = this.StartCreatingCSEntryChangesAsync();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
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
            logger.Trace($"Producing page {this.Batch}");

            while (!this.importContext.ImportItems.IsCompleted || this.importContext.CancellationTokenSource.IsCancellationRequested)
            {
                count++;
                CSEntryChange csentry = null;

                try
                {
                    csentry = this.importContext.ImportItems.Take();
                    this.importContext.ImportedItemCount++;

                    logger.Trace($"Got record {this.importContext.ImportedItemCount}:{csentry.ErrorCodeImport}:{csentry?.ObjectModificationType}:{csentry?.ObjectType}:{csentry?.DN}");
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
                logger.Trace($"Page {this.Batch} complete");
            }
            else
            {
                logger.Info("CSEntryChange consumption complete");
                this.Batch = 0;
            }

            results.MoreToImport = mayHaveMore;
            return Task.FromResult(results);
        }

        public Task<CloseImportConnectionResults> CloseImportConnectionAsync(CloseImportConnectionRunStep importRunStep)
        {
            logger.Info("Closing import connection: {0}", importRunStep.Reason);

            if (this.importContext == null)
            {
                logger.Trace("No import context detected");
                return Task.FromResult(new CloseImportConnectionResults());
            }

            this.importContext.Timer.Stop();

            if (importRunStep.Reason != CloseReason.Normal)
            {
                if (this.importContext.CancellationTokenSource != null)
                {
                    logger.Info("Cancellation request received");
                    this.importContext.CancellationTokenSource.Cancel();
                    this.importContext.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    logger.Info("Cancellation completed");
                }
            }

            logger.Info("Import operation complete");
            logger.Info($"Imported {this.importContext.ImportedItemCount} objects");

            if (this.importContext.ImportedItemCount > 0 && this.importContext.Timer.Elapsed.TotalSeconds > 0)
            {
                if (this.importContext.ProducerDuration.TotalSeconds > 0)
                {
                    logger.Info($"CSEntryChange production duration: {this.importContext.ProducerDuration}");
                    logger.Info($"CSEntryChange production speed: {(this.importContext.ImportedItemCount / this.importContext.ProducerDuration.TotalSeconds):N2} obj/sec");
                }

                logger.Info($"Import duration: {this.importContext.Timer.Elapsed}");
                logger.Info($"Import speed: {(this.importContext.ImportedItemCount / this.importContext.Timer.Elapsed.TotalSeconds):N2} obj/sec");
            }

            if (this.importContext.OutgoingWatermark?.Any() == true)
            {
                string wm = JsonConvert.SerializeObject(this.importContext.OutgoingWatermark);
                logger.Trace($"Watermark: {wm}");
                return Task.FromResult(new CloseImportConnectionResults(wm));
            }
            else
            {
                return Task.FromResult(new CloseImportConnectionResults());
            }
        }

        private Task StartCreatingCSEntryChangesAsync()
        {
            logger.Info("Starting producer thread");

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
                logger.Info("Producer thread cancelled");
            }
            catch (Exception ex)
            {
                logger.Info("Producer thread encountered an exception");
                logger.Error(ex);
                throw;
            }
            finally
            {
                this.importContext.ProducerDuration = this.importContext.Timer.Elapsed;
                logger.Info("CSEntryChange production complete");
                this.importContext.ImportItems.CompleteAdding();
            }

            return Task.CompletedTask;
        }

        private async Task CreateCSEntryChangesForTypeAsync(SchemaType type)
        {
            IObjectImportProvider provider = await this.GetProviderForTypeAsync(type);
            logger.Info($"Starting import of type {type.Name}");
            await provider.InitializeAsync(this.importContext);
            await provider.GetCSEntryChangesAsync(type);
            logger.Info($"Import of type {type.Name} completed");
        }

        private async Task<IObjectImportProvider> GetProviderForTypeAsync(SchemaType type)
        {
            foreach (IObjectImportProvider provider in InterfaceManager.GetInstancesOfType<IObjectImportProvider>())
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