using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Context initializers are called before each import, export or password operation to allow the management agent to perform any required initialization. While import and export providers are each initialized at the start of the operation, context initializers are called only once prior to all operations.
    /// </summary>
    public interface IContextInitializer
    {
        /// <summary>
        /// Initializes the context prior to any import providers being initialized
        /// </summary>
        /// <param name="context">The ImportContext to be shared by the import providers</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InitializeImportAsync(ImportContext context);

        /// <summary>
        /// Initializes the context prior to any export providers being initialized
        /// </summary>
        /// <param name="context">The ExportContext to be shared by the export providers</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InitializeExportAsync(ExportContext context);

        /// <summary>
        /// Initializes the context prior to any password providers being initialized
        /// </summary>
        /// <param name="context">The PasswordContext to be shared by the password providers</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InitializePasswordOperationAsync(PasswordContext context);
    }
}
