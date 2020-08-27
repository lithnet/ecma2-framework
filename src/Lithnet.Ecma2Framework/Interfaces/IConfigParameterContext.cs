using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParameterContext
    {
        KeyedCollection<string, ConfigParameter> ConfigParameters { get; }
    }
}