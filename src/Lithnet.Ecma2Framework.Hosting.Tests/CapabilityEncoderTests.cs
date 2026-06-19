using Lithnet.Ecma2Framework;
using Lithnet.Ecma2Framework.Hosting.Manifest;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    [TestClass]
    public sealed class CapabilityEncoderTests
    {
        // The "all off" baseline encoding for an explicitly all-off MACapabilities:
        // every feature flag false, all support flags off, DistinguishedNameStyle = None.
        //   0x4        (!SupportImport)
        // | 0x8        (!SupportHierarchy)
        // | 0x80       (!SupportExport)
        // | 0x1000000  (!SupportPartitions)
        // | 0x20000 | 0x40000 | 0x80000000  (ECMA2 baseline)
        private const uint AllOffExpected = 0x4u | 0x8u | 0x80u | 0x1000000u | 0x20000u | 0x40000u | 0x80000000u;

        // An explicitly all-off MACapabilities. The host-faithful constructor now enables several flags
        // by default (rename/delta/delete-add/hierarchy/partitions/password/import/export), so a "default"
        // MACapabilities no longer encodes to AllOffExpected. This factory sets every flag off explicitly
        // so the encoder's all-off output can be asserted independently of the constructor defaults.
        private static MACapabilities NewAllOff()
        {
            return new MACapabilities
            {
                SupportImport = false,
                SupportExport = false,
                SupportHierarchy = false,
                SupportPartitions = false,
                SupportPassword = false,
                ObjectRename = false,
                NoReferenceValuesInFirstExport = false,
                DeltaImport = false,
                DeleteAddAsReplace = false,
                ConcurrentOperation = false,
                ExportPasswordInFirstPass = false,
                IsDNAsAnchor = false,
                FullExport = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.None,
                Normalizations = MANormalizations.None
            };
        }

        // ----- Sample regression tests (the ground truth). -----
        // These three are real, observed capability encodings: the encoder MUST reproduce each exactly.
        // 1) Probe   - AllSettingsProbe, read from the live FIM DB (capabilities_mask = 0x810780B8).
        // 2) Okta    - real shipped Okta packaged-MA manifest (capability-bits = 2164758584 = 0x81079838).
        // 3) Google  - real shipped Google packaged-MA manifest (capability-bits = 0x80061A30).
        // Every field is set explicitly so the assertion does not depend on the host-faithful ctor defaults.

        [TestMethod]
        public void ProbeRegression_ProducesValidatedBits()
        {
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = false,
                SupportHierarchy = false,
                SupportPartitions = false,
                SupportPassword = false,
                ConcurrentOperation = true,
                DeltaImport = false,
                DeleteAddAsReplace = true,
                ObjectRename = false,
                IsDNAsAnchor = false,
                NoReferenceValuesInFirstExport = false,
                FullExport = false,
                ExportPasswordInFirstPass = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                Normalizations = MANormalizations.None
            };

            Assert.AreEqual(0x810780B8u, CapabilityEncoder.GetCapabilityBits(caps));
            Assert.AreEqual(2164752568u, CapabilityEncoder.GetCapabilityBits(caps));
        }

        [TestMethod]
        public void OktaRegression_ProducesValidatedBits()
        {
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportHierarchy = false,
                SupportPartitions = false,
                SupportPassword = true,
                ConcurrentOperation = true,
                DeltaImport = true,
                DeleteAddAsReplace = true,
                ObjectRename = false,
                IsDNAsAnchor = false,
                NoReferenceValuesInFirstExport = false,
                FullExport = false,
                ExportPasswordInFirstPass = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                Normalizations = MANormalizations.None,
                ExportType = MAExportType.MultivaluedReferenceAttributeUpdate
            };

            Assert.AreEqual(0x81079838u, CapabilityEncoder.GetCapabilityBits(caps));
            Assert.AreEqual(2164758584u, CapabilityEncoder.GetCapabilityBits(caps));
            Assert.AreEqual(5, CapabilityEncoder.GetExportType(caps));
        }

        [TestMethod]
        public void GoogleRegression_ProducesValidatedBits()
        {
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportHierarchy = true,
                SupportPartitions = true,
                SupportPassword = true,
                ConcurrentOperation = false,
                DeltaImport = true,
                DeleteAddAsReplace = false,
                ObjectRename = true,
                IsDNAsAnchor = false,
                NoReferenceValuesInFirstExport = false,
                FullExport = false,
                ExportPasswordInFirstPass = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                Normalizations = MANormalizations.None
            };

            Assert.AreEqual(0x80061A30u, CapabilityEncoder.GetCapabilityBits(caps));
            Assert.AreEqual(2147883568u, CapabilityEncoder.GetCapabilityBits(caps));
        }

        [TestMethod]
        public void BaselineOnly_AllOffCapabilities_ProducesAllOffEncoding()
        {
            MACapabilities caps = NewAllOff();

            Assert.AreEqual(AllOffExpected, CapabilityEncoder.GetCapabilityBits(caps));
        }

        // ----- CONFIRMED per-flag isolation (validated by the three samples above). -----

        [TestMethod]
        public void ConcurrentOperation_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.ConcurrentOperation = true, 0x8000u);
        }

        [TestMethod]
        public void DeleteAddAsReplace_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.DeleteAddAsReplace = true, 0x10000u);
        }

        [TestMethod]
        public void ObjectRename_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.ObjectRename = true, 0x200u);
        }

        [TestMethod]
        public void SupportPassword_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.SupportPassword = true, 0x800u);
        }

        [TestMethod]
        public void DeltaImport_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.DeltaImport = true, 0x1000u);
        }

        [TestMethod]
        public void DnStyle_None_SetsNoDnBits()
        {
            MACapabilities baseCaps = NewBase();
            baseCaps.DistinguishedNameStyle = MADistinguishedNameStyle.None;
            uint noneBits = CapabilityEncoder.GetCapabilityBits(baseCaps);

            // No DN bits (0x10, 0x20, 0x40) should be set.
            Assert.AreEqual(0u, noneBits & (0x10u | 0x20u | 0x40u));
        }

        [TestMethod]
        public void DnStyle_Generic_SetsBits10And20()
        {
            MACapabilities noneCaps = NewBase();
            noneCaps.DistinguishedNameStyle = MADistinguishedNameStyle.None;

            MACapabilities genericCaps = NewBase();
            genericCaps.DistinguishedNameStyle = MADistinguishedNameStyle.Generic;

            uint delta = CapabilityEncoder.GetCapabilityBits(genericCaps) - CapabilityEncoder.GetCapabilityBits(noneCaps);
            Assert.AreEqual(0x10u | 0x20u, delta);
        }

        // ----- Per-flag isolation validated by the TestAllProbe engine output (see the regression below). -----

        [TestMethod]
        public void DnStyle_Ldap_SetsBits10And20And40()
        {
            // LDAP is a SUPERSET of Generic: the engine emits NativeDnNames | HierarchicalDnNames |
            // LDAPStyleDnNames (0x70), confirmed by the TestAllProbe engine output (mask 0x921E0471).
            MACapabilities noneCaps = NewBase();
            noneCaps.DistinguishedNameStyle = MADistinguishedNameStyle.None;

            MACapabilities ldapCaps = NewBase();
            ldapCaps.DistinguishedNameStyle = MADistinguishedNameStyle.Ldap;

            uint delta = CapabilityEncoder.GetCapabilityBits(ldapCaps) - CapabilityEncoder.GetCapabilityBits(noneCaps);
            Assert.AreEqual(0x10u | 0x20u | 0x40u, delta);
        }

        [TestMethod]
        public void NoReferenceValuesInFirstExport_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.NoReferenceValuesInFirstExport = true, 0x400u);
        }

        [TestMethod]
        public void Normalizations_Uppercase_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.Normalizations = MANormalizations.Uppercase, 0x80000u);
        }

        [TestMethod]
        public void Normalizations_RemoveAccents_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.Normalizations = MANormalizations.RemoveAccents, 0x100000u);
        }

        [TestMethod]
        public void Normalizations_UppercaseAndRemoveAccents_SetsBothBits()
        {
            // MANormalizations is [Flags]; both flags set => both bits set (the equality/else-if bug
            // dropped both). Confirmed by the TestAllProbe engine output (0x80000 | 0x100000).
            AssertFlagTogglesBit(c => c.Normalizations = MANormalizations.Uppercase | MANormalizations.RemoveAccents, 0x80000u | 0x100000u);
        }

        [TestMethod]
        public void FullExport_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.FullExport = true, 0x2000000u);
        }

        [TestMethod]
        public void ExportPasswordInFirstPass_TogglesOnlyItsBit()
        {
            AssertFlagTogglesBit(c => c.ExportPasswordInFirstPass = true, 0x10000000u);
        }

        [TestMethod]
        public void IsDNAsAnchor_TogglesOnlyItsBit_Inferred()
        {
            // Still inferred: no sample sets IsDNAsAnchor.
            AssertFlagTogglesBit(c => c.IsDNAsAnchor = true, 0x40000000u);
        }

        // ----- Capabilities-MASK (bits + the engine-stamped ImmediateExportConfirmation on export). -----

        [TestMethod]
        public void TestAllProbeRegression_ProducesValidatedBitsAndMask()
        {
            // The AllParamsProbe MA, created in FIM with export enabled and read back from the engine:
            // capability-bits 0x921E0470, capabilities-mask 0x921E0471. Exercises LDAP DN (0x10|0x20|0x40),
            // both normalizations together (0x80000|0x100000), NoRef (0x400), FullExport (0x2000000),
            // ExportPasswordInFirstPass (0x10000000), and the mask-only ImmediateExportConfirmation (0x1).
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportPartitions = true,
                SupportPassword = false,
                SupportHierarchy = true,
                ConcurrentOperation = false,
                DeltaImport = false,
                DeleteAddAsReplace = false,
                ObjectRename = false,
                NoReferenceValuesInFirstExport = true,
                FullExport = true,
                ExportPasswordInFirstPass = true,
                IsDNAsAnchor = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.Ldap,
                ExportType = MAExportType.AttributeReplace,
                Normalizations = MANormalizations.Uppercase | MANormalizations.RemoveAccents,
                ObjectConfirmation = MAObjectConfirmation.Normal
            };

            Assert.AreEqual(0x921E0470u, CapabilityEncoder.GetCapabilityBits(caps), "capability-bits must match the engine output.");
            Assert.AreEqual(0x921E0471u, CapabilityEncoder.GetCapabilitiesMask(caps), "capabilities-mask must be bits | ImmediateExportConfirmation.");
        }

        [TestMethod]
        public void GetCapabilitiesMask_ImportOnly_EqualsBits()
        {
            // Import-only probe (clean engine output): no export => mask == bits == 0x810780B8.
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = false,
                SupportHierarchy = false,
                SupportPartitions = false,
                SupportPassword = false,
                ConcurrentOperation = true,
                DeltaImport = false,
                DeleteAddAsReplace = true,
                ObjectRename = false,
                IsDNAsAnchor = false,
                NoReferenceValuesInFirstExport = false,
                FullExport = false,
                ExportPasswordInFirstPass = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                Normalizations = MANormalizations.None
            };

            Assert.AreEqual(0x810780B8u, CapabilityEncoder.GetCapabilityBits(caps));
            Assert.AreEqual(0x810780B8u, CapabilityEncoder.GetCapabilitiesMask(caps), "import-only mask must equal the bits (no ImmediateExportConfirmation).");
        }

        [TestMethod]
        public void GetCapabilitiesMask_Export_SetsImmediateExportConfirmation()
        {
            // Real shipped Okta caps (export + password): bits 0x81079838, mask 0x81079839 = bits | 0x1.
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportHierarchy = false,
                SupportPartitions = false,
                SupportPassword = true,
                ConcurrentOperation = true,
                DeltaImport = true,
                DeleteAddAsReplace = true,
                ObjectRename = false,
                IsDNAsAnchor = false,
                NoReferenceValuesInFirstExport = false,
                FullExport = false,
                ExportPasswordInFirstPass = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.Generic,
                Normalizations = MANormalizations.None,
                ExportType = MAExportType.MultivaluedReferenceAttributeUpdate
            };

            Assert.AreEqual(0x81079838u, CapabilityEncoder.GetCapabilityBits(caps));
            Assert.AreEqual(0x81079839u, CapabilityEncoder.GetCapabilitiesMask(caps), "export mask must set ImmediateExportConfirmation (0x1).");
        }

        [TestMethod]
        public void ExportType_PassesThrough()
        {
            MACapabilities caps = NewBase();

            caps.ExportType = MAExportType.AttributeUpdate;
            Assert.AreEqual(1, CapabilityEncoder.GetExportType(caps));

            caps.ExportType = MAExportType.AttributeReplace;
            Assert.AreEqual(2, CapabilityEncoder.GetExportType(caps));
        }

        // A fixed, fully-supported base so single-flag isolation tests don't trip the
        // "not supported" bits. Import/export/hierarchy/partitions are all supported,
        // DN style is None, normalizations None.
        //
        // Every feature flag is set EXPLICITLY (including those the host-faithful MACapabilities
        // constructor now enables by default: ObjectRename, DeltaImport, DeleteAddAsReplace,
        // ConcurrentOperation, SupportPassword). The single-bit toggle tests below flip a flag from
        // this known-off base to true and assert exactly one bit changes, so the base must start with
        // every toggled flag OFF rather than relying on constructor defaults.
        private static MACapabilities NewBase()
        {
            return new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportHierarchy = true,
                SupportPartitions = true,
                SupportPassword = false,
                ObjectRename = false,
                NoReferenceValuesInFirstExport = false,
                DeltaImport = false,
                DeleteAddAsReplace = false,
                ConcurrentOperation = false,
                ExportPasswordInFirstPass = false,
                IsDNAsAnchor = false,
                FullExport = false,
                DistinguishedNameStyle = MADistinguishedNameStyle.None,
                Normalizations = MANormalizations.None
            };
        }

        private static void AssertFlagTogglesBit(System.Action<MACapabilities> setFlag, uint expectedBit)
        {
            MACapabilities baseCaps = NewBase();
            uint without = CapabilityEncoder.GetCapabilityBits(baseCaps);

            setFlag(baseCaps);
            uint with = CapabilityEncoder.GetCapabilityBits(baseCaps);

            Assert.AreEqual(expectedBit, with ^ without, "Flag should toggle exactly its bit.");
        }
    }
}
