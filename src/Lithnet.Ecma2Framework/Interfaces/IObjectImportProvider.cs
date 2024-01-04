using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectImportProvider
    {
        void Initialize(IImportContext context);

        bool CanImport(SchemaType type);

        void GetCSEntryChanges(SchemaType type);
    }
}
