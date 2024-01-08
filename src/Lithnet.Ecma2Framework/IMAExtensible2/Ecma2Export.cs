using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Export :
        IMAExtensible2CallExport
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static List<IObjectExportProvider> providerCache;

        private static List<IObjectExportProviderAsync> asyncProviderCache;

        private static List<IObjectExportProvider> Providers
        {
            get
            {
                if (Ecma2Export.providerCache == null)
                {
                    Ecma2Export.providerCache = InterfaceManager.GetInstancesOfType<IObjectExportProvider>().ToList();
                }

                return Ecma2Export.providerCache;
            }
        }

        private static List<IObjectExportProviderAsync> AsyncProviders
        {
            get
            {
                if (Ecma2Export.asyncProviderCache == null)
                {
                    Ecma2Export.asyncProviderCache = InterfaceManager.GetInstancesOfType<IObjectExportProviderAsync>().ToList();
                }

                return Ecma2Export.asyncProviderCache;
            }
        }

        private ExportContext exportContext;

        public int ExportDefaultPageSize => 100;

        public int ExportMaxPageSize => 9999;

        public void OpenExportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            Logging.SetupLogger(configParameters);

            this.exportContext = new ExportContext()
            {
                ConfigParameters = configParameters
            };

            try
            {
                logger.Info("Starting export");
                this.exportContext.ConnectionContext = InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContext(configParameters, ConnectionContextOperationType.Export);
                this.InitializeProviders(this.exportContext);
                this.exportContext.Timer.Start();
            }
            catch (Exception ex)
            {
                logger.Error(ex);
                throw;
            }
        }

        public PutExportEntriesResults PutExportEntries(IList<CSEntryChange> csentries)
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

                logger.Info($"Exporting record {record}");

                try
                {
                    timer.Start();
                    result = this.PutCSEntryChange(csentry);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"An error occurred exporting record {record}");
                    result = CSEntryChangeResult.Create(csentry.Identifier, null, MAExportError.ExportErrorCustomContinueRun, ex.Message, ex.ToString());
                }
                finally
                {
                    timer.Stop();

                    if (result == null)
                    {
                        logger.Error($"CSEntryResult for object {record} was null");
                    }
                    else
                    {
                        lock (results)
                        {
                            results.CSEntryChangeResults.Add(result);
                        }
                    }

                    logger.Trace($"Export of record {record} returned '{result?.ErrorCode.ToString().ToLower() ?? "<null>"}' and took {timer.Elapsed}");
                }
            });

            logger.Info($"Page complete. Export count: {this.exportContext.ExportedItemCount}");
            return results;
        }

        private CSEntryChangeResult PutCSEntryChange(CSEntryChange csentry)
        {
            IObjectExportProviderAsync providerAsync = this.GetAsyncProviderForType(csentry);

            if (providerAsync != null)
            {
                return AsyncHelper.RunSync(providerAsync.PutCSEntryChangeAsync(csentry), this.exportContext.Token);
            }
            else
            {
                IObjectExportProvider provider = this.GetProviderForType(csentry);
                return provider.PutCSEntryChange(csentry);
            }
        }

        private IObjectExportProvider GetProviderForType(CSEntryChange csentry)
        {
            foreach (IObjectExportProvider provider in Ecma2Export.Providers)
            {
                if (provider.CanExport(csentry))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException($"An export provider for the type '{csentry.ObjectType}' could not be found");
        }

        private IObjectExportProviderAsync GetAsyncProviderForType(CSEntryChange csentry)
        {
            foreach (IObjectExportProviderAsync provider in Ecma2Export.AsyncProviders)
            {
                if (provider.CanExport(csentry))
                {
                    return provider;
                }
            }

            return null;
        }

        private void InitializeProviders(ExportContext context)
        {
            foreach (var provider in AsyncProviders)
            {
                provider.Initialize(context);
            }

            foreach (var provider in Providers)
            {
                provider.Initialize(context);
            }
        }

        public void CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
            logger.Info($"Closing export connection: {exportRunStep.Reason}");

            if (this.exportContext == null)
            {
                logger.Trace("No export context detected");
                return;
            }

            this.exportContext.Timer.Stop();

            if (exportRunStep.Reason != CloseReason.Normal)
            {
                if (this.exportContext.CancellationTokenSource != null)
                {
                    logger.Info("Cancellation request received");
                    this.exportContext.CancellationTokenSource.Cancel();
                    this.exportContext.CancellationTokenSource.Token.WaitHandle.WaitOne();
                    logger.Info("Cancellation completed");
                }
            }

            logger.Info("Export operation complete");
            logger.Info($"Exported {this.exportContext.ExportedItemCount} objects");
            logger.Info($"Export duration: {this.exportContext.Timer.Elapsed}");

            if (this.exportContext.ExportedItemCount > 0 && this.exportContext.Timer.Elapsed.TotalSeconds > 0)
            {
                logger.Info($"Speed: {(this.exportContext.ExportedItemCount / this.exportContext.Timer.Elapsed.TotalSeconds):N2} obj/sec");
                logger.Info($"Average: {(this.exportContext.Timer.Elapsed.TotalSeconds / this.exportContext.ExportedItemCount):N2} sec/obj");
            }
        }
    }
}