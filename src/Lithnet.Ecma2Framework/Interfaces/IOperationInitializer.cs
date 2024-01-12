using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework
{
    public interface IOperationInitializer
    {
        Task InitializeImportAsync(IImportContext context);

        Task InitializeExportAsync(IExportContext context);

        Task InitializePasswordOperationAsync(IPasswordContext context);

        Task InitializeSchemaOperationAsync(ISchemaContext context);
    }
}
