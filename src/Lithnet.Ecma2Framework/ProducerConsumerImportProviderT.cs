using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.MetadirectoryServices;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public abstract class ProducerConsumerImportProvider<TObject> : IObjectImportProvider
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        protected IImportContext ImportContext { get; private set; }

        public abstract Task<bool> CanImportAsync(SchemaType type);

        public async Task InitializeAsync(IImportContext context)
        {
            this.ImportContext = context;
            await this.OnInitializeAsync();
        }

        public async Task GetCSEntryChangesAsync(SchemaType type)
        {
            try
            {
                var items = this.GetObjects();
                BufferBlock<TObject> queue = new BufferBlock<TObject>();

                Task consumer = this.ConsumeObjects(type, queue);

                // Post source data to the dataflow block.
                await this.ProduceObjects(items, queue).ConfigureAwait(false);

                // Wait for the consumer to process all data.
                await consumer.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "There was an error importing the item data");
                throw;
            }
        }

        private async Task ProduceObjects(IAsyncEnumerable<TObject> items, ITargetBlock<TObject> target)
        {
            await this.OnStartProducerAsync();
            await items.ForEachAsync(t => target.Post(t));
            target.Complete();

            await this.OnStartProducerAsync();
        }

        private async Task ConsumeObjects(SchemaType type, ISourceBlock<TObject> source)
        {
            await this.OnStartConsumerAsync();

            while (await source.OutputAvailableAsync())
            {
                var item = source.Receive();

                try
                {
                    await this.PrepareObjectForImportAsync(item).ConfigureAwait(false);
                    CSEntryChange c = await this.CreateCSEntryChange(type, item).ConfigureAwait(false);

                    if (c != null)
                    {
                        this.ImportContext.ImportItems.Add(c, this.ImportContext.Token);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex);
                    CSEntryChange csentry = CSEntryChange.Create();
                    csentry.DN = this.GetDN(item);
                    csentry.ErrorCodeImport = MAImportError.ImportErrorCustomContinueRun;
                    csentry.ErrorDetail = ex.StackTrace;
                    csentry.ErrorName = ex.Message;
                    this.ImportContext.ImportItems.Add(csentry, this.ImportContext.Token);
                }

                this.ImportContext.Token.ThrowIfCancellationRequested();
            }

            await this.OnCompleteConsumerAsync();
        }

        protected virtual Task PrepareObjectForImportAsync(TObject item)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnStartProducerAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnCompleteProducerAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnStartConsumerAsync()
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnCompleteConsumerAsync()
        {
            return Task.CompletedTask;
        }

        protected abstract string GetDN(TObject item);

        protected abstract Task<CSEntryChange> CreateCSEntryChange(SchemaType schemaType, TObject item);

        protected abstract IAsyncEnumerable<TObject> GetObjects();

        protected abstract ObjectModificationType GetObjectModificationType(TObject item);
    }
}
