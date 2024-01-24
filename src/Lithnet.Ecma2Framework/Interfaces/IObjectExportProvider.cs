using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines the methods and properties that an object export provider must implement
    /// </summary>
    public interface IObjectExportProvider
    {
        /// <summary>
        /// Initializes the object export provider. This method is called once at the start of an export operation
        /// </summary>
        /// <param name="context">The context of the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InitializeAsync(ExportContext context);

        /// <summary>
        /// Indicates whether the object export provider can export objects of the specified type
        /// </summary>
        /// <param name="csentry">The CSEntryChange representing the object to be exported</param>
        /// <returns><see langword="true"/> if the provider can export the object, otherwise <see langword="false"/> </returns>
        Task<bool> CanExportAsync(CSEntryChange csentry);

        /// <summary>
        /// Exports the specified object
        /// </summary>
        /// <param name="csentry">The CSEntryChange representing the object to be exported</param>
        /// <param name="cancellationToken">A cancellation token</param>    
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<CSEntryChangeResult> PutCSEntryChangeAsync(CSEntryChange csentry, CancellationToken cancellationToken);
    }
}