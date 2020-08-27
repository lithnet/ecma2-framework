using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ISchemaContext : IConfigParameterContext
    {
        IConnectionContext ConnectionContext { get; }
    }
}