using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Example
{
    internal class CapabilitiesProvider : ICapabilitiesProvider
    {
        /// <summary>
        /// Gets the capabilities of the management agent
        /// </summary>
        /// <param name="configParameters">The configuration parameters for the management agent</param>
        /// <returns>A MACapabilities object representing the capabilities of the management agent</returns>
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
