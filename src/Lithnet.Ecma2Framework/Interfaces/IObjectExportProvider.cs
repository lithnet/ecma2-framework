using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectExportProvider
    {
        void Initialize(IExportContext context);

        bool CanExport(CSEntryChange csentry);

        CSEntryChangeResult PutCSEntryChange(CSEntryChange csentry);
    }
}