using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ISchemaContext
    {
        IConnectionContext ConnectionContext { get; }

        KeyedCollection<string, ConfigParameter> ConfigParameters { get; }
    }
}