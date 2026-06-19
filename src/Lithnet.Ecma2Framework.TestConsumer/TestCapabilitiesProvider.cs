using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    public sealed class TestCapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters)
        {
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportPassword = true,
            };
            return Task.FromResult(caps);
        }
    }
}
