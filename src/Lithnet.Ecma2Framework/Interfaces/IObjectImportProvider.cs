using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines the methods and properties that an object import provider must implement
    /// </summary>
    public interface IObjectImportProvider
    {
        /// <summary>
        /// Initializes the object import provider. This method is called once at the start of an import operation
        /// </summary>
        /// <param name="context">The context of the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InitializeAsync(ImportContext context);

        /// <summary>
        /// Indicates whether the object import provider can import objects of the specified type
        /// </summary>
        /// <param name="type">The type of object to be imported</param>
        /// <returns><see langword="true"/> if the provider can import the object, otherwise <see langword="false"/> </returns>
        Task<bool> CanImportAsync(SchemaType type);

        /// <summary>
        /// Initiates the operation to import objects of the specified type. Created CSEntryChanges should be added to the provided ICSEntryChangeCollection object.
        /// </summary>
        /// <param name="type">The type of object to import</param>
        /// <param name="csentryCollection">The collection of CSEntryChange objects to add the imported objects to</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetCSEntryChangesAsync(SchemaType type, ICSEntryChangeCollection csentryCollection, string incomingWatermark, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the outbound watermark to save to the synchronization service at the completion of the import operation
        /// If the management agent doesn't support delta operations, then this method should return null
        /// </summary>
        /// <param name="type">The object type to get the watermark for</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <returns>The outbound watermark, or null if the management agent doesn't support delta operations</returns>
        Task<string> GetOutboundWatermark(SchemaType type, CancellationToken cancellationToken);
    }
}
