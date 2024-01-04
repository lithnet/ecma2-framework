using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ISchemaProvider
    {
        Schema GetMmsSchema(SchemaContext context);
    }
}
