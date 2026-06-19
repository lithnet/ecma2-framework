using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.PackageTests
{
    /// <summary>
    /// Reports the package-test MA's capabilities. It advertises import support only. Exists to satisfy the
    /// generator's ICapabilitiesProvider discovery requirement (ECMA2003).
    /// </summary>
    public sealed class PackageTestCapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters)
        {
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = false,
                SupportPassword = false,
                SupportHierarchy = false,
                SupportPartitions = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.None,
                ObjectRename = false,
                DeltaImport = false,
                DeleteAddAsReplace = false,
                ConcurrentOperation = false,
                Normalizations = MANormalizations.None,
                ObjectConfirmation = MAObjectConfirmation.Normal,
                ExportType = MAExportType.ObjectReplace,
                NoReferenceValuesInFirstExport = false,
                FullExport = false,
                ExportPasswordInFirstPass = false,
                IsDNAsAnchor = false,
            };
            return Task.FromResult(caps);
        }
    }
}
