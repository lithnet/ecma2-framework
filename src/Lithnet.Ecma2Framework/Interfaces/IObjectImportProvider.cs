using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectImportProvider
    {
        Task InitializeAsync(IImportContext context);

        Task<bool> CanImportAsync(SchemaType type);

        Task GetCSEntryChangesAsync(SchemaType type);
    }
}
