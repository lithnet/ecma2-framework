using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public abstract class ProducerConsumerImportProvider<TObject> : IObjectImportProvider
    {
        private readonly ILogger logger;

        protected ImportContext ImportContext { get; private set; }

        public ProducerConsumerImportProvider(ILogger logger)
        {
            this.logger = logger;
        }

        public abstract Task<bool> CanImportAsync(SchemaType type);

        public async Task InitializeAsync(ImportContext context)
        {
            this.ImportContext = context;
            try
            {
                await this.OnInitializeAsync();
            }
            catch (NotImplementedException) { }
        }

        public async Task GetCSEntryChangesAsync(SchemaType type)
        {
            try
            {
                var items = this.GetObjects();
                BufferBlock<TObject> queue = new BufferBlock<TObject>();

                Task consumer = this.ConsumeObjectsAsync(type, queue);

                // Post source data to the dataflow block.
                await this.ProduceObjectsAsync(items, queue).ConfigureAwait(false);

                // Wait for the consumer to process all data.
                await consumer.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "There was an error importing the item data");
                throw;
            }
        }

        private async Task ProduceObjectsAsync(IAsyncEnumerable<TObject> items, ITargetBlock<TObject> target)
        {
            try
            {
                await this.OnStartProducerAsync();
            }
            catch (NotImplementedException) { }

            await items.ForEachAsync(t => target.Post(t));
            target.Complete();

            try
            {
                await this.OnStartProducerAsync();
            }
            catch (NotImplementedException) { }
        }

        private async Task ConsumeObjectsAsync(SchemaType type, ISourceBlock<TObject> source)
        {
            try
            {
                await this.OnStartConsumerAsync();
            }
            catch (NotImplementedException) { }

            while (await source.OutputAvailableAsync())
            {
                var item = source.Receive();

                try
                {
                    try
                    {
                        await this.PrepareObjectForImportAsync(item).ConfigureAwait(false);
                    }
                    catch (NotImplementedException) { }

                    CSEntryChange c = await this.CreateCSEntryChangeAsync(type, item).ConfigureAwait(false);

                    if (c != null)
                    {
                        this.ImportContext.ImportItems.Add(c, this.ImportContext.Token);
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error creating CSEntryChange");
                    CSEntryChange csentry = CSEntryChange.Create();
                    csentry.DN = await this.GetDNAsync(item);
                    csentry.ErrorCodeImport = MAImportError.ImportErrorCustomContinueRun;
                    csentry.ErrorDetail = ex.StackTrace;
                    csentry.ErrorName = ex.Message;
                    this.ImportContext.ImportItems.Add(csentry, this.ImportContext.Token);
                }

                this.ImportContext.Token.ThrowIfCancellationRequested();
            }

            try
            {
                await this.OnCompleteConsumerAsync();
            }
            catch (NotImplementedException) { }
        }

        protected virtual async Task<CSEntryChange> CreateCSEntryChangeAsync(SchemaType schemaType, TObject item)
        {
            try
            {
                this.logger.LogTrace($"Creating CSEntryChange for {item}");

                ObjectModificationType modType = await this.GetObjectModificationTypeAsync(item);

                if (modType == ObjectModificationType.None)
                {
                    return null;
                }

                CSEntryChange c = CSEntryChange.Create();
                c.ObjectType = schemaType.Name;
                c.ObjectModificationType = modType;
                c.DN = await this.GetDNAsync(item);

                try
                {
                    await this.OnPrepareCSEntryChangeAsync(c);
                }
                catch (NotImplementedException) { }

                foreach (var anchor in await this.GetAnchorAttributesAsync(item))
                {
                    c.AnchorAttributes.Add(anchor);
                }

                if (modType == ObjectModificationType.Delete)
                {
                    try
                    {
                        await this.OnFinalizeCsEntryChangeAsync(c);
                    }
                    catch (NotImplementedException) { }

                    return c;
                }

                foreach (SchemaAttribute type in schemaType.Attributes)
                {
                    var change = await this.CreateAttributeChangeAsync(type, modType, item);

                    if (change != null)
                    {
                        c.AttributeChanges.Add(change);
                    }
                }

                try
                {
                    await this.OnFinalizeCsEntryChangeAsync(c);
                }
                catch (NotImplementedException) { }

                return c;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error creating CSEntryChange for {item}");
                throw;
            }
        }

        protected virtual Task OnFinalizeCsEntryChangeAsync(CSEntryChange csentry)
        {
            return Task.CompletedTask;
        }

        protected virtual Task OnPrepareCSEntryChangeAsync(CSEntryChange csentry)
        {
            return Task.CompletedTask;
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

        protected abstract Task<List<AnchorAttribute>> GetAnchorAttributesAsync(TObject item);

        protected abstract Task<AttributeChange> CreateAttributeChangeAsync(SchemaAttribute type, ObjectModificationType modificationType, TObject item);

        protected abstract Task<string> GetDNAsync(TObject item);

        protected abstract IAsyncEnumerable<TObject> GetObjects();

        protected abstract Task<ObjectModificationType> GetObjectModificationTypeAsync(TObject item);
    }
}
