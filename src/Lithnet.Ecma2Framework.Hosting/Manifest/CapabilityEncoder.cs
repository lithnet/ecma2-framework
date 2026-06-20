using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.Manifest
{
    /// <summary>
    /// Reproduces FIM's computation of the <c>&lt;capability-bits&gt;</c> (a uint bitfield) and
    /// <c>&lt;export-type&gt;</c> (an int) that the metadirectory service normally derives from a
    /// management agent's <see cref="MACapabilities"/> when it registers a connector.
    /// <para>
    /// The encoding is the internal <c>[Flags] enum MACapabilities</c> decompiled from FIM's
    /// <c>mahostm.dll</c>. The native public-class-to-flags conversion lives in <c>mmsmaext.dll</c>
    /// (not decompilable), so this maps the mirror <see cref="MACapabilities"/> (host-faithful
    /// defaults) onto those flags.
    /// </para>
    /// <para>
    /// The engine builds the flags from THREE sources, all reproduced here: (a) the public
    /// <see cref="MACapabilities"/> properties; (b) intrinsic, interface-driven bits the engine stamps
    /// from which <c>IMAExtensible2*</c> interfaces the extension implements -- <c>GetClassSchema</c>
    /// (GetSchema) in the bits, and <c>ImmediateExportConfirmation</c> (CallExport) in the MASK only,
    /// see <see cref="GetCapabilitiesMask"/>; and (c) two ECMA2 framework baseline constants. The
    /// mappings are validated bit-for-bit against clean engine output (the import-only probe
    /// 0x810780B8, and the export probe AllParamsProbe bits 0x921E0470 / mask 0x921E0471) and the
    /// shipped Okta (0x81079838) and Google (0x80061A30) manifests. Only <c>DnAsAnchor</c> (IsDNAsAnchor)
    /// remains "not sample-validated" -- no sample sets it.
    /// </para>
    /// </summary>
    internal static class CapabilityEncoder
    {
        // ----- "Not supported" bits: NEGATIVE flags, set when the corresponding capability is absent. -----
        private const uint NotSupportImport = 0x4;
        private const uint NotSupportHierarchy = 0x8;
        private const uint NotSupportExport = 0x80;
        private const uint NotSupportPartitions = 0x1000000;

        // ----- Distinguished-name style bits (additive, not mutually exclusive). -----
        // These are independent capability bits, not an enum: Generic sets NativeDnNames (0x10) +
        // HierarchicalDnNames (0x20); Ldap is a SUPERSET that adds LDAPStyleDnNames (0x40) on top, i.e.
        // 0x10 | 0x20 | 0x40 (0x70); None sets nothing. Confirmed by the TestAllProbe engine output
        // (Ldap -> 0x70) and the Generic samples (Okta/Google/probe -> 0x30).
        private const uint NativeDnNames = 0x10;
        private const uint HierarchicalDnNames = 0x20;
        private const uint LdapStyleDnNames = 0x40;

        // ----- Feature bits (all sample-validated unless noted). -----
        private const uint LeafRename = 0x200;            // ObjectRename
        private const uint NoRefExportFirstPass = 0x400;  // NoReferenceValuesInFirstExport (TestAllProbe)
        private const uint SupportPassword = 0x800;
        private const uint DeltaImport = 0x1000;
        private const uint ConcurrentExecution = 0x8000;  // ConcurrentOperation
        private const uint FullReplaceOnDelete = 0x10000; // DeleteAddAsReplace (a SINGLE bit)

        // Normalizations: MANormalizations IS [Flags] (verified against the host assembly), so Uppercase
        // and RemoveAccents combine and are tested independently. Both confirmed by the TestAllProbe
        // engine output (which set both -> 0x80000 | 0x100000).
        private const uint NormalizeToUppercase = 0x80000;       // Normalizations Uppercase
        private const uint NormalizeRemoveAccents = 0x100000;    // Normalizations RemoveAccents

        private const uint OptionalFullExport = 0x2000000;       // FullExport (TestAllProbe)
        private const uint ExportPasswordInFirstPass = 0x10000000; // ExportPasswordInFirstPass (TestAllProbe)
        private const uint DnAsAnchor = 0x40000000;              // IsDNAsAnchor // not sample-validated

        // Mask-only bit. The engine stamps ImmediateExportConfirmation (0x1) into the capabilities-MASK
        // for an export-capable ECMA2 MA (one that implements IMAExtensible2CallExport). It is NOT derived
        // from any public MACapabilities property (the host class exposes none), so it NEVER appears in the
        // extension-declared <capability-bits> - only in the <capabilities-mask>. See GetCapabilitiesMask.
        // The flag's exact semantics are not authoritatively documented (the [MS-UPSDBDAP] prose is
        // preliminary/unreliable); this mirrors the OBSERVED engine behaviour, validated bit-for-bit.
        private const uint ImmediateExportConfirmation = 0x1;

        // Intrinsic, interface-/framework-driven bits the engine sets independent of the MACapabilities
        // property values. GetClassSchema is set because the v3 shim always implements IMAExtensible2GetSchema
        // (interface-driven, the same category as ImmediateExportConfirmation); AttributeUpdateListOnExport and
        // CaseNormalizationDnForAnchor are the ECMA2 framework baseline. All three are present in every real
        // sample (import-only and export alike).
        //   AttributeUpdateListOnExport (0x20000)
        // | GetClassSchema             (0x40000)
        // | CaseNormalizationDnForAnchor (0x80000000)
        private const uint Baseline = 0x20000 | 0x40000 | 0x80000000;

        /// <summary>
        /// Computes the FIM capability-bits uint bitfield for the supplied capabilities.
        /// </summary>
        /// <param name="capabilities">The management agent capabilities to encode.</param>
        /// <returns>The capability-bits value as FIM would compute it.</returns>
        public static uint GetCapabilityBits(MACapabilities capabilities)
        {
            uint bits = Baseline;

            if (!capabilities.SupportImport)
            {
                bits |= NotSupportImport;
            }

            if (!capabilities.SupportHierarchy)
            {
                bits |= NotSupportHierarchy;
            }

            if (!capabilities.SupportExport)
            {
                bits |= NotSupportExport;
            }

            if (!capabilities.SupportPartitions)
            {
                bits |= NotSupportPartitions;
            }

            if (capabilities.DistinguishedNameStyle == MADistinguishedNameStyle.Generic)
            {
                bits |= NativeDnNames | HierarchicalDnNames;
            }
            else if (capabilities.DistinguishedNameStyle == MADistinguishedNameStyle.Ldap)
            {
                // LDAP is a superset of Generic: the engine sets the native + hierarchical bits too.
                bits |= NativeDnNames | HierarchicalDnNames | LdapStyleDnNames;
            }

            if (capabilities.ObjectRename)
            {
                bits |= LeafRename;
            }

            if (capabilities.NoReferenceValuesInFirstExport)
            {
                bits |= NoRefExportFirstPass;
            }

            if (capabilities.SupportPassword)
            {
                bits |= SupportPassword;
            }

            if (capabilities.DeltaImport)
            {
                bits |= DeltaImport;
            }

            if (capabilities.ConcurrentOperation)
            {
                bits |= ConcurrentExecution;
            }

            if (capabilities.DeleteAddAsReplace)
            {
                bits |= FullReplaceOnDelete;
            }

            // MANormalizations is [Flags]; the two flags combine, so test each independently rather than
            // by equality (a combined Uppercase|RemoveAccents value must set BOTH bits).
            if (capabilities.Normalizations.HasFlag(MANormalizations.Uppercase))
            {
                bits |= NormalizeToUppercase;
            }

            if (capabilities.Normalizations.HasFlag(MANormalizations.RemoveAccents))
            {
                bits |= NormalizeRemoveAccents;
            }

            if (capabilities.FullExport)
            {
                bits |= OptionalFullExport;
            }

            if (capabilities.ExportPasswordInFirstPass)
            {
                bits |= ExportPasswordInFirstPass;
            }

            // not sample-validated
            if (capabilities.IsDNAsAnchor)
            {
                bits |= DnAsAnchor;
            }

            return bits;
        }

        /// <summary>
        /// Computes the FIM <c>capabilities-mask</c> for the supplied capabilities. This is the
        /// <see cref="GetCapabilityBits"/> value plus the engine-stamped
        /// <c>ImmediateExportConfirmation</c> (0x1) bit when the MA supports export.
        /// </summary>
        /// <remarks>
        /// FIM bakes this mask into a packaged-MA manifest as the sticky, trusted capability value, so it
        /// must match what the engine itself computes when it registers an equivalent MA. The engine sets
        /// <c>ImmediateExportConfirmation</c> for any export-capable ECMA2 MA (which always implements
        /// <c>IMAExtensible2CallExport</c>); it is absent for import-only MAs. Both cases are validated
        /// bit-for-bit against clean engine output: import-only mask == bits == <c>0x810780B8</c>, and the
        /// export probe mask == bits | 0x1 == <c>0x921E0471</c> (also matching the shipped Okta package
        /// <c>0x81079838</c> -> <c>0x81079839</c>).
        /// </remarks>
        /// <param name="capabilities">The management agent capabilities to encode.</param>
        /// <returns>The capabilities-mask value as FIM would compute it.</returns>
        public static uint GetCapabilitiesMask(MACapabilities capabilities)
        {
            uint mask = GetCapabilityBits(capabilities);

            if (capabilities.SupportExport)
            {
                mask |= ImmediateExportConfirmation;
            }

            return mask;
        }

        /// <summary>
        /// Returns the FIM export-type value, which is the raw <see cref="MAExportType"/> enum value.
        /// </summary>
        /// <param name="capabilities">The management agent capabilities to encode.</param>
        /// <returns>The export type as an int.</returns>
        public static int GetExportType(MACapabilities capabilities)
        {
            return (int)capabilities.ExportType;
        }
    }
}
