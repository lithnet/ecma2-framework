using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// <para>The ProducerConsumerImportProvider class provides a base class for import providers that use a producer-consumer pattern. </para>
    /// <para>
    /// This class provides a simple implementation of the producer-consumer pattern, and allows the developer to focus on the import logic, rather than the threading logic.
    /// Implementers implement the mandatory methods that enumerate the raw objects of type TObject, and provide the logic to convert these objects into CSEntryChange objects. The provider takes care of constructing the CSEntryChanges and passing them back to the sync engine.
    /// </para>
    /// </summary>
    /// <typeparam name="TObject">The type of object that the provider will enumerate</typeparam>
    public abstract class ProducerConsumerImportProvider<TObject> : IObjectImportProvider
    {
        private readonly ILogger logger;

        /// <summary>
        /// Gets the import context used for this operation
        /// </summary>
        protected ImportContext ImportContext { get; private set; }

        /// <summary>
        /// Initializes a new instance of the ProducerConsumerImportProvider class
        /// </summary>
        /// <param name="logger"></param>
        protected ProducerConsumerImportProvider(ILogger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Gets a value indicating whether the provider can import objects of the specified type
        /// </summary>
        /// <param name="type">The type of object to be imported</param>
        /// <returns><see langword="true"/> if the provider can import the object, otherwise <see langword="false"/> </returns>
        public abstract Task<bool> CanImportAsync(SchemaType type);

        /// <summary>
        /// Initializes the object import provider. This method is called once at the start of an import operation
        /// </summary>
        /// <param name="context">The context of the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task InitializeAsync(ImportContext context)
        {
            this.ImportContext = context;
            try
            {
                await this.OnInitializeAsync();
            }
            catch (NotImplementedException) { }
        }

        /// <inheritdoc/>
        public async Task GetCSEntryChangesAsync(SchemaType type, ICSEntryChangeCollection collection, string watermark, CancellationToken cancellationToken)
        {
            try
            {
                var items = this.GetObjectsAsync(watermark, cancellationToken);
                BufferBlock<TObject> queue = new BufferBlock<TObject>();

                Task consumer = this.ConsumeObjectsAsync(type, queue, collection, cancellationToken);

                // Post source data to the dataflow block.
                await this.ProduceObjectsAsync(items, queue, cancellationToken).ConfigureAwait(false);

                // Wait for the consumer to process all data.
                await consumer.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "There was an error importing the item data");
                throw;
            }
        }
        
        /// <inheritdoc/>
        public abstract Task<string> GetOutboundWatermark(SchemaType type, CancellationToken cancellationToken);

        /// <summary>
        /// Enumerates objects of type TObject and posts them to the target block
        /// </summary>
        /// <param name="items">An enumerable of objects to be imported</param>
        /// <param name="target">The target block to post the objects to</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task ProduceObjectsAsync(IAsyncEnumerable<TObject> items, ITargetBlock<TObject> target, CancellationToken cancellationToken)
        {
            try
            {
                await this.OnStartProducerAsync();
            }
            catch (NotImplementedException) { }

            await items.ForEachAsync(t => target.Post(t), cancellationToken);
            target.Complete();

            try
            {
                await this.OnCompleteProducerAsync();
            }
            catch (NotImplementedException) { }
        }

        /// <summary>
        /// Consumes objects of type TObject from the source block and creates CSEntryChange objects
        /// </summary>
        /// <param name="type">The schema type of the objects being imported</param>
        /// <param name="source">The source block to consume objects from</param>
        /// <param name="target">The target collection to add the CSEntryChange objects to</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task ConsumeObjectsAsync(SchemaType type, ISourceBlock<TObject> source, ICSEntryChangeCollection target, CancellationToken cancellationToken)
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
                        await this.PrepareObjectForImportAsync(item, cancellationToken).ConfigureAwait(false);
                    }
                    catch (NotImplementedException) { }

                    CSEntryChange c = await this.CreateCSEntryChangeAsync(type, item, cancellationToken).ConfigureAwait(false);

                    if (c != null)
                    {
                        target.AddCSEntryChange(c);
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
                    target.AddCSEntryChange(csentry);
                }

                cancellationToken.ThrowIfCancellationRequested();
            }

            try
            {
                await this.OnCompleteConsumerAsync();
            }
            catch (NotImplementedException) { }
        }

        /// <summary>
        /// Creates a CSEntryChange object from an object of type TObject
        /// </summary>
        /// <param name="schemaType">The schema type of the object being imported</param>
        /// <param name="item">The object to be imported</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        private async Task<CSEntryChange> CreateCSEntryChangeAsync(SchemaType schemaType, TObject item, CancellationToken cancellationToken)
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
                    await this.OnPrepareCSEntryChangeAsync(c, cancellationToken);
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
                        await this.OnFinalizeCsEntryChangeAsync(c, cancellationToken);
                    }
                    catch (NotImplementedException) { }

                    return c;
                }

                foreach (SchemaAttribute type in schemaType.Attributes)
                {
                    var change = await this.CreateAttributeChangeAsync(type, modType, item, cancellationToken);

                    if (change != null)
                    {
                        c.AttributeChanges.Add(change);
                    }
                }

                try
                {
                    await this.OnFinalizeCsEntryChangeAsync(c, cancellationToken);
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

        /// <summary>
        /// A method that is called when the CSEntryChange has been completed, but before it is returned to the sync engine.
        /// Override this method to inspect or modify the CSEntryChange before it is returned to the sync engine
        /// </summary>
        /// <param name="csentry">The CSEntryChange object that has been created</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnFinalizeCsEntryChangeAsync(CSEntryChange csentry, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the CSEntryChange has been created, but before any attribute changes have been added to it.
        /// Override this method to inspect or modify the CSEntryChange before any attributes have been added to it
        /// </summary>
        /// <param name="csentry">The CSEntryChange object that has been created</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnPrepareCSEntryChangeAsync(CSEntryChange csentry, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the object has been retrieved from the source, but before it is converted into a CSEntryChange object.
        /// Override this method to inspect or modify the object before it is converted into a CSEntryChange object
        /// </summary>
        /// <param name="item">The object that has been retrieved from the source</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task PrepareObjectForImportAsync(TObject item, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the provider is initialized, but before any objects are retrieved from the source.
        /// Override this method to perform any initialization logic
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the provider is initialized, but before any objects are retrieved from the source.
        /// Override this method to perform any initialization logic required to start producing objects
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnStartProducerAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the provider has finished producing objects.
        /// Override this method to perform any cleanup logic required after all objects have been produced
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnCompleteProducerAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the provider has started consuming objects.
        /// Override this method to perform any initialization logic required to start consuming objects
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnStartConsumerAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// A method that is called when the provider has finished consuming objects.
        /// Override this method to perform any cleanup logic required after all objects have been consumed and the provider is about to be terminated
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        protected virtual Task OnCompleteConsumerAsync()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets the list of AnchorAttributes for the specified object
        /// </summary>
        /// <param name="item">The object to get the AnchorAttributes for</param>
        /// <returns>A list of AnchorAttributes</returns>
        protected abstract Task<List<AnchorAttribute>> GetAnchorAttributesAsync(TObject item);

        /// <summary>
        /// Creates an AttributeChange object for the specified attribute
        /// </summary>
        /// <param name="type">The schema attribute to create the AttributeChange for</param>
        /// <param name="modificationType">The modification type of the object</param>
        /// <param name="item">The object to create the AttributeChange from</param>
        /// <param name="cancellationToken">A cancellation token</param>
        ///<returns>An AttributeChange represnting the specified attribute, or null if there are no changes to the attribute provided</returns>
        protected abstract Task<AttributeChange> CreateAttributeChangeAsync(SchemaAttribute type, ObjectModificationType modificationType, TObject item, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the DN of the specified object. This will be used to populate the DN property of the CSEntryChange
        /// </summary>
        /// <param name="item">The item to return the DN of</param>
        /// <returns>The DN that will be used in the CSEntryChange</returns>
        protected abstract Task<string> GetDNAsync(TObject item);

        /// <summary>
        /// Gets the objects to be imported.
        /// </summary>
        /// <param name="watermark">The watermark value provided by the management agent after its last successful import</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>An enumerable of objects to be imported</returns>
        protected abstract IAsyncEnumerable<TObject> GetObjectsAsync(string watermark, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the modification type of the specified object. This will be used to determine the object modification type of the CSEntryChange
        /// </summary>
        /// <param name="item">The object to get the modification type of</param>
        /// <returns>The modification type of the object</returns>
        protected abstract Task<ObjectModificationType> GetObjectModificationTypeAsync(TObject item);
    }
}
