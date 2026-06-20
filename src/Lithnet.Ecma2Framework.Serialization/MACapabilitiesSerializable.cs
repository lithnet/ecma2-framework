using System;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real MACapabilities. The host ctor seeds 13 members to non-default values
    // (host-surface-inventory.md / host-to-model-mapping.md), so the wire MUST carry all 17 members
    // explicitly and GetObject MUST assign every one — never let a ctor default silently survive over a
    // consumer's intentional false/0. EmitDefaultValue stays true (the DataContract default) so a false or
    // an enum value of 0 is not dropped. Assignment order honours the DistinguishedNameStyle setter
    // side-effect (None forces ObjectRename=false): DistinguishedNameStyle is assigned first and
    // ObjectRename last, so a genuine None re-derives ObjectRename=false while a non-None value preserves
    // the carried ObjectRename.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class MACapabilitiesSerializable
#else
    public class MACapabilitiesSerializable
#endif
    {
        [DataMember]
        public MADistinguishedNameStyle DistinguishedNameStyle { get; set; }

        [DataMember]
        public bool ObjectRename { get; set; }

        [DataMember]
        public bool NoReferenceValuesInFirstExport { get; set; }

        [DataMember]
        public bool DeltaImport { get; set; }

        [DataMember]
        public bool ConcurrentOperation { get; set; }

        [DataMember]
        public bool DeleteAddAsReplace { get; set; }

        [DataMember]
        public bool ExportPasswordInFirstPass { get; set; }

        [DataMember]
        public bool FullExport { get; set; }

        [DataMember]
        public MAObjectConfirmation ObjectConfirmation { get; set; }

        [DataMember]
        public MAExportType ExportType { get; set; }

        [DataMember]
        public MANormalizations Normalizations { get; set; }

        [DataMember]
        public bool IsDNAsAnchor { get; set; }

        [DataMember]
        public bool SupportImport { get; set; }

        [DataMember]
        public bool SupportExport { get; set; }

        [DataMember]
        public bool SupportPartitions { get; set; }

        [DataMember]
        public bool SupportPassword { get; set; }

        [DataMember]
        public bool SupportHierarchy { get; set; }

        internal MACapabilitiesSerializable(MACapabilities capabilities)
        {
            this.SetObject(capabilities);
        }

        internal void SetObject(MACapabilities capabilities)
        {
            this.DistinguishedNameStyle = capabilities.DistinguishedNameStyle;
            this.ObjectRename = capabilities.ObjectRename;
            this.NoReferenceValuesInFirstExport = capabilities.NoReferenceValuesInFirstExport;
            this.DeltaImport = capabilities.DeltaImport;
            this.ConcurrentOperation = capabilities.ConcurrentOperation;
            this.DeleteAddAsReplace = capabilities.DeleteAddAsReplace;
            this.ExportPasswordInFirstPass = capabilities.ExportPasswordInFirstPass;
            this.FullExport = capabilities.FullExport;
            this.ObjectConfirmation = capabilities.ObjectConfirmation;
            this.ExportType = capabilities.ExportType;
            this.Normalizations = capabilities.Normalizations;
            this.IsDNAsAnchor = capabilities.IsDNAsAnchor;
            this.SupportImport = capabilities.SupportImport;
            this.SupportExport = capabilities.SupportExport;
            this.SupportPartitions = capabilities.SupportPartitions;
            this.SupportPassword = capabilities.SupportPassword;
            this.SupportHierarchy = capabilities.SupportHierarchy;
        }

        internal MACapabilities GetObject()
        {
            MACapabilities capabilities = new MACapabilities();

            // DistinguishedNameStyle first (its setter zeroes ObjectRename when None); ObjectRename last so a
            // carried true is never clobbered by an earlier non-None DN style assignment.
            capabilities.DistinguishedNameStyle = this.DistinguishedNameStyle;
            capabilities.NoReferenceValuesInFirstExport = this.NoReferenceValuesInFirstExport;
            capabilities.DeltaImport = this.DeltaImport;
            capabilities.ConcurrentOperation = this.ConcurrentOperation;
            capabilities.DeleteAddAsReplace = this.DeleteAddAsReplace;
            capabilities.ExportPasswordInFirstPass = this.ExportPasswordInFirstPass;
            capabilities.FullExport = this.FullExport;
            capabilities.ObjectConfirmation = this.ObjectConfirmation;
            capabilities.ExportType = this.ExportType;
            capabilities.Normalizations = this.Normalizations;
            capabilities.IsDNAsAnchor = this.IsDNAsAnchor;
            capabilities.SupportImport = this.SupportImport;
            capabilities.SupportExport = this.SupportExport;
            capabilities.SupportPartitions = this.SupportPartitions;
            capabilities.SupportPassword = this.SupportPassword;
            capabilities.SupportHierarchy = this.SupportHierarchy;
            capabilities.ObjectRename = this.ObjectRename;

            return capabilities;
        }
    }
}
