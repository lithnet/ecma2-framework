using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.SampleConsumer
{
    /// <summary>
    /// Reports the sample MA's capabilities. The sample only exercises import and schema, so it advertises
    /// import support and leaves export/password/hierarchy/partitions off.
    /// </summary>
    /// <remarks>
    /// Every capability is set EXPLICITLY rather than relying on the host-faithful <see cref="MACapabilities"/>
    /// constructor defaults (which enable rename/delta/delete-add/hierarchy/partitions). This keeps the sample's
    /// effective capabilities — and therefore the encoded <c>capability-bits</c> exercised by the packaged-MA
    /// end-to-end test — deterministic and independent of those defaults.
    /// </remarks>
    public sealed class SampleCapabilitiesProvider : ICapabilitiesProvider
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
