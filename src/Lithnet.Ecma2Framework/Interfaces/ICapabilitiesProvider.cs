using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ICapabilitiesProvider
    {
        MACapabilities GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters);
    }
}
