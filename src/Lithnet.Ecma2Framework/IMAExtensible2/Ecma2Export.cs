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

        private ExportContext exportContext;

        public int ExportDefaultPageSize => 100;

        public int ExportMaxPageSize => 9999;

        public void OpenExportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            Logging.SetupLogger(configParameters);

            this.exportContext = new ExportContext()
            {
                CancellationTokenSource = new CancellationTokenSource(),
                ConfigParameters = configParameters
            };

            try
            {
                logger.Info("Starting export");
                this.exportContext.ConnectionContext = InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContext(configParameters, ConnectionContextOperationType.Export);
                this.exportContext.Timer.Start();
            }
            catch (Exception ex)
            {
                logger.Error(ex.UnwrapIfSingleAggregateException());
                throw;
            }
        }

        public PutExportEntriesResults PutExportEntries(IList<CSEntryChange> csentries)
        {
            PutExportEntriesResults results = new PutExportEntriesResults();

            ParallelOptions po = new ParallelOptions
            {
                MaxDegreeOfParallelism = GlobalSettings.ExportThreadCount,
                CancellationToken = this.exportContext.CancellationTokenSource.Token
            };

            Parallel.ForEach(csentries, po, (csentry) =>
            {
                Stopwatch timer = new Stopwatch();

                Interlocked.Increment(ref this.exportContext.ExportedItemCount);
                logger.Info("Performing export for " + csentry.DN);
                try
                {
                    IObjectExportProvider provider = this.GetProviderForType(csentry);
                    timer.Start();
                    CSEntryChangeResult result = provider.PutCSEntryChange(csentry, this.exportContext);
                    timer.Stop();
                    lock (results)
                    {
                        results.CSEntryChangeResults.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    timer.Stop();
                    logger.Error(ex.UnwrapIfSingleAggregateException());
                    lock (results)
                    {
                        results.CSEntryChangeResults.Add(CSEntryChangeResult.Create(csentry.Identifier, null, MAExportError.ExportErrorCustomContinueRun, ex.UnwrapIfSingleAggregateException().Message, ex.UnwrapIfSingleAggregateException().ToString()));
                    }
                }
                finally
                {
                    logger.Trace($"Export of {csentry.DN} took {timer.Elapsed}");
                }
            });

            return results;
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

        public void CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
            logger.Info("Closing export connection: {0}", exportRunStep.Reason);

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
            logger.Info("Exported {0} objects", this.exportContext.ExportedItemCount);
            logger.Info("Export duration: {0}", this.exportContext.Timer.Elapsed);
            if (this.exportContext.ExportedItemCount > 0 && this.exportContext.Timer.Elapsed.TotalSeconds > 0)
            {
                logger.Info("Speed: {0} obj/sec", (int)(this.exportContext.ExportedItemCount / this.exportContext.Timer.Elapsed.TotalSeconds));
                logger.Info("Average: {0} sec/obj", this.exportContext.Timer.Elapsed.TotalSeconds / this.exportContext.ExportedItemCount);
            }
        }
    }
}