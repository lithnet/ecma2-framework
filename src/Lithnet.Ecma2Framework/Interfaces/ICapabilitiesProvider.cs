using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ICapabilitiesProvider
    {
        Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters);
    }
}
