using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework
{
    public interface IConnectionInitializer
    {
        Task InitializeImportAsync(IImportContext context);

        Task InitializeExportAsync(IExportContext context);

        Task InitializePasswordOperationAsync(IPasswordContext context);
    }
}
