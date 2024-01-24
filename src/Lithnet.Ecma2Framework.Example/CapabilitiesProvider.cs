using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Example
{
    public class CapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters)
        {
            return Task.FromResult(
                new MACapabilities
                {
                    ConcurrentOperation = true,
                    DeltaImport = false,
                    DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                    ExportType = MAExportType.ObjectReplace,
                    SupportExport = true,
                    SupportImport = true
                });
        }
    }
}
