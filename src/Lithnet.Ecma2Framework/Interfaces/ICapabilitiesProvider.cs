using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ICapabilitiesProvider
    {
        Task<MACapabilities> GetCapabilitiesAsync(KeyedCollection<string, ConfigParameter> configParameters);
    }
}
