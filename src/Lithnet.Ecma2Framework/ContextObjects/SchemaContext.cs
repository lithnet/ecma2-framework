using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class SchemaContext : ISchemaContext
    {
        public IConnectionContext ConnectionContext { get; internal set; }

        public KeyedCollection<string, ConfigParameter> ConfigParameters { get; internal set; }

        public object CustomData { get; set; }
    }
}
