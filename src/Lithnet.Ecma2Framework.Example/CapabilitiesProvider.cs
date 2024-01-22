using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Example
{
    public class CapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesExAsync(KeyedCollection<string, ConfigParameter> configParameters)
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
