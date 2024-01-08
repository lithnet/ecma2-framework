using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectExportProvider
    {
        Task InitializeAsync(IExportContext context);

        Task<bool> CanExportAsync(CSEntryChange csentry);

        Task<CSEntryChangeResult> PutCSEntryChangeAsync(CSEntryChange csentry);
    }
}