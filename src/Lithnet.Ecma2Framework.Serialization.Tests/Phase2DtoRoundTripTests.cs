using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    [TestClass]
    public class Phase2DtoRoundTripTests
    {
        [TestMethod]
        public void MACapabilitiesRoundTripsAnInvariantViolatingCombo()
        {
            // Build a real MACapabilities where every member is flipped away from the ctor default, AND set an
            // invariant-tension combo: DistinguishedNameStyle=None (whose setter forces ObjectRename=false) but
            // the surviving wire state must reflect what the host actually holds. After setting None the host
            // ObjectRename is false; the round-trip must preserve that exactly (not resurrect a true).
            MACapabilities caps = new MACapabilities();
            caps.DistinguishedNameStyle = MADistinguishedNameStyle.Ldap;
            caps.ObjectRename = false;                 // away from ctor default (true)
            caps.NoReferenceValuesInFirstExport = true; // away from default (false)
            caps.DeltaImport = false;                  // away from default (true)
            caps.ConcurrentOperation = false;          // away from default (true)
            caps.DeleteAddAsReplace = false;           // away from default (true)
            caps.ExportPasswordInFirstPass = true;     // away from default (false)
            caps.FullExport = true;                    // away from default (false)
            caps.ObjectConfirmation = MAObjectConfirmation.NoAddAndDeleteConfirmation;
            caps.ExportType = MAExportType.MultivaluedReferenceAttributeUpdate;
            caps.Normalizations = MANormalizations.Uppercase | MANormalizations.RemoveAccents;
            caps.IsDNAsAnchor = true;                  // away from default (false)
            caps.SupportImport = false;                // away from default (true)
            caps.SupportExport = false;                // away from default (true)
            caps.SupportPartitions = false;            // away from default (true)
            caps.SupportPassword = false;              // away from default (true)
            caps.SupportHierarchy = false;             // away from default (true)

            string xml = MmsPipeSerializer.SerializeXml<MACapabilities>(caps);
            MACapabilities rt = MmsPipeSerializer.DeserializeXml<MACapabilities>(xml);

            Assert.AreEqual(MADistinguishedNameStyle.Ldap, rt.DistinguishedNameStyle, "DistinguishedNameStyle");
            Assert.IsFalse(rt.ObjectRename, "ObjectRename (intentional false must survive over the ctor default true)");
            Assert.IsTrue(rt.NoReferenceValuesInFirstExport, "NoReferenceValuesInFirstExport");
            Assert.IsFalse(rt.DeltaImport, "DeltaImport (intentional false must survive)");
            Assert.IsFalse(rt.ConcurrentOperation, "ConcurrentOperation (intentional false must survive)");
            Assert.IsFalse(rt.DeleteAddAsReplace, "DeleteAddAsReplace (intentional false must survive)");
            Assert.IsTrue(rt.ExportPasswordInFirstPass, "ExportPasswordInFirstPass");
            Assert.IsTrue(rt.FullExport, "FullExport");
            Assert.AreEqual(MAObjectConfirmation.NoAddAndDeleteConfirmation, rt.ObjectConfirmation, "ObjectConfirmation");
            Assert.AreEqual(MAExportType.MultivaluedReferenceAttributeUpdate, rt.ExportType, "ExportType");
            Assert.AreEqual(MANormalizations.Uppercase | MANormalizations.RemoveAccents, rt.Normalizations, "Normalizations (flags)");
            Assert.IsTrue(rt.IsDNAsAnchor, "IsDNAsAnchor");
            Assert.IsFalse(rt.SupportImport, "SupportImport (intentional false must survive)");
            Assert.IsFalse(rt.SupportExport, "SupportExport (intentional false must survive)");
            Assert.IsFalse(rt.SupportPartitions, "SupportPartitions (intentional false must survive)");
            Assert.IsFalse(rt.SupportPassword, "SupportPassword (intentional false must survive)");
            Assert.IsFalse(rt.SupportHierarchy, "SupportHierarchy (intentional false must survive)");
        }

        [TestMethod]
        public void MACapabilitiesPreservesObjectRenameTrueWithNonNoneStyle()
        {
            // The assignment-order hazard: a carried ObjectRename=true with a non-None DistinguishedNameStyle
            // must NOT be clobbered. (GetObject assigns DistinguishedNameStyle first, ObjectRename last.)
            MACapabilities caps = new MACapabilities();
            caps.DistinguishedNameStyle = MADistinguishedNameStyle.Generic;
            caps.ObjectRename = true;

            string xml = MmsPipeSerializer.SerializeXml<MACapabilities>(caps);
            MACapabilities rt = MmsPipeSerializer.DeserializeXml<MACapabilities>(xml);

            Assert.AreEqual(MADistinguishedNameStyle.Generic, rt.DistinguishedNameStyle);
            Assert.IsTrue(rt.ObjectRename, "ObjectRename=true must survive a non-None DN style assignment");
        }

        [TestMethod]
        public void ConfigParameterPlaintextRoundTrips()
        {
            ConfigParameter p = new ConfigParameter("server", "okta.example.com");

            string xml = MmsPipeSerializer.SerializeXml<ConfigParameter>(p);
            ConfigParameter rt = MmsPipeSerializer.DeserializeXml<ConfigParameter>(xml);

            Assert.AreEqual("server", rt.Name, "Name");
            Assert.AreEqual("okta.example.com", rt.Value, "Value");
            Assert.IsFalse(rt.IsEncrypted, "IsEncrypted");
        }

        [TestMethod]
        public void ConfigParameterEcma1EncryptedRoundTrips()
        {
            // ECMA1-style encrypted: isEncrypted=true, usesSecureString=false -> Value getter is legal.
            ConfigParameter p = new ConfigParameter("legacySecret", "cipherblob", true);

            string xml = MmsPipeSerializer.SerializeXml<ConfigParameter>(p);
            ConfigParameter rt = MmsPipeSerializer.DeserializeXml<ConfigParameter>(xml);

            Assert.AreEqual("legacySecret", rt.Name, "Name");
            Assert.AreEqual("cipherblob", rt.Value, "Value");
            Assert.IsTrue(rt.IsEncrypted, "IsEncrypted");
        }

        [TestMethod]
        public void ConfigParameterSecureValueRoundTripsTheSecret()
        {
            // SecureString-backed encrypted param: the secret content must survive the round-trip.
            SecureString secret = new SecureString();
            foreach (char c in "Sup3r$ecret!")
            {
                secret.AppendChar(c);
            }

            ConfigParameter p = new ConfigParameter("apiToken", secret);

            string xml = MmsPipeSerializer.SerializeXml<ConfigParameter>(p);
            ConfigParameter rt = MmsPipeSerializer.DeserializeXml<ConfigParameter>(xml);

            Assert.AreEqual("apiToken", rt.Name, "Name");
            Assert.IsTrue(rt.IsEncrypted, "IsEncrypted");

            // SecureValue is the only legal accessor here; assert the secret content survived.
            SecureString rtSecure = rt.SecureValue;
            Assert.IsNotNull(rtSecure, "SecureValue");
            Assert.AreEqual("Sup3r$ecret!", Unsecure(rtSecure), "secret content preserved across the round-trip");
        }

        [TestMethod]
        public void ConfigParameterDefinitionDropDownRoundTripsRawValidation()
        {
            // DropDown carries comma-escaped values in Validation; the raw field must round-trip without
            // double-escaping (factory-only reconstruction would re-escape). Reconstructed via the private
            // six-field ctor.
            ConfigParameterDefinition def = ConfigParameterDefinition.CreateDropDownParameter(
                "region",
                new[] { "us, east", "eu, west" },
                true,
                "us, east");

            string rawValidation = def.Validation;

            string xml = MmsPipeSerializer.SerializeXml<ConfigParameterDefinition>(def);
            ConfigParameterDefinition rt = MmsPipeSerializer.DeserializeXml<ConfigParameterDefinition>(xml);

            Assert.AreEqual("region", rt.Name, "Name");
            Assert.AreEqual(ConfigParameterType.DropDown, rt.Type, "Type");
            Assert.AreEqual(rawValidation, rt.Validation, "Validation (raw comma-escaped, no double-escape)");
            Assert.AreEqual("us, east", rt.DefaultValue, "DefaultValue");
            Assert.IsTrue(rt.DropDownExtensible, "DropDownExtensible");
        }

        [TestMethod]
        public void ConfigParameterDefinitionCheckBoxRoundTrips()
        {
            ConfigParameterDefinition def = ConfigParameterDefinition.CreateCheckBoxParameter("enabled", true);

            string xml = MmsPipeSerializer.SerializeXml<ConfigParameterDefinition>(def);
            ConfigParameterDefinition rt = MmsPipeSerializer.DeserializeXml<ConfigParameterDefinition>(xml);

            Assert.AreEqual("enabled", rt.Name, "Name");
            Assert.AreEqual(ConfigParameterType.CheckBox, rt.Type, "Type");
            Assert.AreEqual("1", rt.DefaultValue, "DefaultValue");
            Assert.IsTrue(rt.CheckBoxDefault, "CheckBoxDefault (computed from DefaultValue)");
        }

        [TestMethod]
        public void ConfigParameterDefinitionNonFactoryShapeRoundTrips()
        {
            // A shape no public factory can produce (a String param with a non-empty Text). Build it via the
            // host's private six-field master ctor, then prove a full serializer round-trip carries it
            // losslessly (the DTO reconstructs via that same private ctor).
            ConfigParameterDefinition def = CreateViaMasterCtor(
                ConfigParameterType.String,
                "user",
                "a non-empty text on a String param",
                "^.+$",
                "admin",
                false);

            string xml = MmsPipeSerializer.SerializeXml<ConfigParameterDefinition>(def);
            ConfigParameterDefinition rt = MmsPipeSerializer.DeserializeXml<ConfigParameterDefinition>(xml);

            Assert.AreEqual("user", rt.Name);
            Assert.AreEqual(ConfigParameterType.String, rt.Type);
            Assert.AreEqual("a non-empty text on a String param", rt.Text, "non-factory Text preserved");
            Assert.AreEqual("^.+$", rt.Validation);
            Assert.AreEqual("admin", rt.DefaultValue);
        }

        private static ConfigParameterDefinition CreateViaMasterCtor(
            ConfigParameterType type, string name, string text, string validation, string defaultValue, bool userExtensible)
        {
            System.Reflection.ConstructorInfo ctor = typeof(ConfigParameterDefinition).GetConstructor(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(ConfigParameterType), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool) },
                null);

            return (ConfigParameterDefinition)ctor.Invoke(new object[] { type, name, text, validation, defaultValue, userExtensible });
        }

        [TestMethod]
        public void ParameterValidationResultRoundTrips()
        {
            ParameterValidationResult result = new ParameterValidationResult(
                ParameterValidationResultCode.Failure,
                "the region is invalid",
                "region");

            string xml = MmsPipeSerializer.SerializeXml<ParameterValidationResult>(result);
            ParameterValidationResult rt = MmsPipeSerializer.DeserializeXml<ParameterValidationResult>(xml);

            Assert.AreEqual(ParameterValidationResultCode.Failure, rt.Code, "Code");
            Assert.AreEqual("the region is invalid", rt.ErrorMessage, "ErrorMessage");
            Assert.AreEqual("region", rt.ErrorParameter, "ErrorParameter");
        }

        [TestMethod]
        public void PartitionRoundTripsIncludingHiddenByDefault()
        {
            Guid id = Guid.NewGuid();
            Partition partition = Partition.Create(id, "DC=example,DC=com", "Example");
            partition.HiddenByDefault = true;

            string xml = MmsPipeSerializer.SerializeXml<Partition>(partition);
            Partition rt = MmsPipeSerializer.DeserializeXml<Partition>(xml);

            Assert.AreEqual(id, rt.Identifier, "Identifier");
            Assert.AreEqual("DC=example,DC=com", rt.DN, "DN");
            Assert.AreEqual("Example", rt.Name, "Name");
            Assert.IsTrue(rt.HiddenByDefault, "HiddenByDefault (prior wire-drop hazard)");
        }

        [TestMethod]
        public void HierarchyNodeRoundTrips()
        {
            HierarchyNode node = HierarchyNode.Create("OU=Sales,DC=example,DC=com", "Sales");

            string xml = MmsPipeSerializer.SerializeXml<HierarchyNode>(node);
            HierarchyNode rt = MmsPipeSerializer.DeserializeXml<HierarchyNode>(xml);

            Assert.AreEqual("OU=Sales,DC=example,DC=com", rt.DN, "DN");
            Assert.AreEqual("Sales", rt.DisplayName, "DisplayName");
        }

        [TestMethod]
        public void OpenImportConnectionRunStepRoundTrips()
        {
            Partition partition = Partition.Create(Guid.NewGuid(), "DC=example,DC=com", "Example");
            IList<HierarchyNode> inclusion = new List<HierarchyNode> { HierarchyNode.Create("OU=A", "A") };
            IList<HierarchyNode> exclusion = new List<HierarchyNode> { HierarchyNode.Create("OU=B", "B") };

            OpenImportConnectionRunStep step = new OpenImportConnectionRunStep(
                partition,
                OperationType.Delta,
                250,
                "watermark-cookie",
                inclusion,
                exclusion);

            string xml = MmsPipeSerializer.SerializeXml<OpenImportConnectionRunStep>(step);
            OpenImportConnectionRunStep rt = MmsPipeSerializer.DeserializeXml<OpenImportConnectionRunStep>(xml);

            Assert.AreEqual(OperationType.Delta, rt.ImportType, "ImportType (explicit, not the ctor-default Full)");
            Assert.AreEqual(250, rt.PageSize, "PageSize (explicit, not the ctor-default 1)");
            Assert.AreEqual("watermark-cookie", rt.CustomData, "CustomData");
            Assert.IsNotNull(rt.StepPartition, "StepPartition");
            Assert.AreEqual("Example", rt.StepPartition.Name, "StepPartition.Name");
            Assert.AreEqual(1, rt.InclusionHierarchyNodes.Count, "InclusionHierarchyNodes count");
            Assert.AreEqual("OU=A", rt.InclusionHierarchyNodes.First().DN, "inclusion node DN");
            Assert.AreEqual(1, rt.ExclusionHierarchyNodes.Count, "ExclusionHierarchyNodes count");
            Assert.AreEqual("OU=B", rt.ExclusionHierarchyNodes.First().DN, "exclusion node DN");
        }

        [TestMethod]
        public void OpenExportConnectionRunStepRoundTrips()
        {
            Partition partition = Partition.Create(Guid.NewGuid(), "DC=example,DC=com", "Example");

            OpenExportConnectionRunStep step = new OpenExportConnectionRunStep(
                partition,
                100,
                OperationType.FullObject,
                new List<HierarchyNode> { HierarchyNode.Create("OU=A", "A") },
                new List<HierarchyNode>());

            string xml = MmsPipeSerializer.SerializeXml<OpenExportConnectionRunStep>(step);
            OpenExportConnectionRunStep rt = MmsPipeSerializer.DeserializeXml<OpenExportConnectionRunStep>(xml);

            Assert.AreEqual(OperationType.FullObject, rt.ExportType, "ExportType");
            Assert.AreEqual(100, rt.BatchSize, "BatchSize (explicit, not the ctor-default 1)");
            Assert.AreEqual("Example", rt.StepPartition.Name, "StepPartition.Name");
            Assert.AreEqual(1, rt.InclusionHierarchyNodes.Count, "InclusionHierarchyNodes count");
            Assert.AreEqual(0, rt.ExclusionHierarchyNodes.Count, "ExclusionHierarchyNodes count");
        }

        [TestMethod]
        public void CloseImportConnectionRunStepRoundTrips()
        {
            CloseImportConnectionRunStep step = new CloseImportConnectionRunStep(
                CloseReason.TerminatedByUser,
                "final-watermark");

            string xml = MmsPipeSerializer.SerializeXml<CloseImportConnectionRunStep>(step);
            CloseImportConnectionRunStep rt = MmsPipeSerializer.DeserializeXml<CloseImportConnectionRunStep>(xml);

            Assert.AreEqual(CloseReason.TerminatedByUser, rt.Reason, "Reason (explicit, not the ctor-default Normal)");
            Assert.AreEqual("final-watermark", rt.CustomData, "CustomData");
        }

        [TestMethod]
        public void CloseExportConnectionRunStepRoundTrips()
        {
            CloseExportConnectionRunStep step = new CloseExportConnectionRunStep(CloseReason.TerminatedShuttingDown);

            string xml = MmsPipeSerializer.SerializeXml<CloseExportConnectionRunStep>(step);
            CloseExportConnectionRunStep rt = MmsPipeSerializer.DeserializeXml<CloseExportConnectionRunStep>(xml);

            Assert.AreEqual(CloseReason.TerminatedShuttingDown, rt.Reason, "Reason (explicit, not the ctor-default Normal)");
        }

        [TestMethod]
        public void GetImportEntriesRunStepRoundTripsItsEntries()
        {
            CSEntryChange cse = CSEntryChange.Create();
            cse.ObjectModificationType = ObjectModificationType.Add;
            cse.DN = "CN=jdoe";
            cse.ObjectType = "person";
            cse.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("displayName", "John Doe"));

            GetImportEntriesRunStep step = new GetImportEntriesRunStep(
                new List<CSEntryChange> { cse },
                "page-cookie");

            string xml = MmsPipeSerializer.SerializeXml<GetImportEntriesRunStep>(step);
            GetImportEntriesRunStep rt = MmsPipeSerializer.DeserializeXml<GetImportEntriesRunStep>(xml);

            Assert.AreEqual("page-cookie", rt.CustomData, "CustomData");
            Assert.AreEqual(1, rt.FullObjectEntries.Count, "FullObjectEntries count");
            Assert.AreEqual("CN=jdoe", rt.FullObjectEntries.First().DN, "entry DN");
            Assert.AreEqual("person", rt.FullObjectEntries.First().ObjectType, "entry ObjectType");
            Assert.IsTrue(rt.FullObjectEntries.First().AttributeChanges.Contains("displayName"), "entry attribute carried");
        }

        [TestMethod]
        public void SchemaCarriesChangeTypeObjectTypeAttributeNameAndHiddenByDefault()
        {
            // Members the ported ACMA SchemaSerializable previously dropped, now carried (surfaced by the
            // completeness guard): Schema.ChangeType, Schema.ObjectTypeAttributeName, and
            // SchemaAttribute.HiddenByDefault.
            ChangeTypeDescription changeType = new ChangeTypeDescriptionDetachedProbe("changeType", "add", "modify", "delete");
            Schema schema = new Schema(changeType, "objectClass");

            SchemaType person = SchemaType.Create("person", false);
            SchemaAttribute hidden = SchemaAttribute.CreateSingleValuedAttribute("secret", AttributeType.String, AttributeOperation.ImportOnly);
            hidden.HiddenByDefault = true;
            person.Attributes.Add(hidden);
            schema.Types.Add(person);

            string xml = MmsPipeSerializer.SerializeXml<Schema>(schema);
            Schema rt = MmsPipeSerializer.DeserializeXml<Schema>(xml);

            Assert.IsNotNull(rt.ChangeType, "ChangeType (ACMA DTO previously dropped this)");
            Assert.AreEqual("changeType", rt.ChangeType.AttributeName, "ChangeType.AttributeName");
            Assert.AreEqual("add", rt.ChangeType.Add, "ChangeType.Add");
            Assert.AreEqual("modify", rt.ChangeType.Modify, "ChangeType.Modify");
            Assert.AreEqual("delete", rt.ChangeType.Delete, "ChangeType.Delete");
            Assert.AreEqual("objectClass", rt.ObjectTypeAttributeName, "ObjectTypeAttributeName (previously dropped)");

            SchemaAttribute rtHidden = rt.Types.First().Attributes.First(a => a.Name == "secret");
            Assert.IsTrue(rtHidden.HiddenByDefault, "SchemaAttribute.HiddenByDefault (previously dropped)");
        }

        [TestMethod]
        public void SchemaTypeCarriesPossibleDNComponentsForProvisioning()
        {
            // A real mutable host list the ported ACMA DTO previously dropped (surfaced by the guard).
            Schema schema = Schema.Create();
            SchemaType person = SchemaType.Create("person", false);
            person.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String, AttributeOperation.ImportOnly));
            person.PossibleDNComponentsForProvisioning.Add("CN");
            person.PossibleDNComponentsForProvisioning.Add("OU");
            schema.Types.Add(person);

            string xml = MmsPipeSerializer.SerializeXml<Schema>(schema);
            Schema rt = MmsPipeSerializer.DeserializeXml<Schema>(xml);

            SchemaType rtPerson = rt.Types.First();
            CollectionAssert.AreEqual(
                new[] { "CN", "OU" },
                rtPerson.PossibleDNComponentsForProvisioning.ToArray(),
                "PossibleDNComponentsForProvisioning (previously dropped by the ACMA DTO)");
        }

        // A constructible ChangeTypeDescription for the test (the host type is abstract with no public ctor).
        private sealed class ChangeTypeDescriptionDetachedProbe : ChangeTypeDescription
        {
            private readonly string attributeName;
            private readonly string add;
            private readonly string modify;
            private readonly string delete;

            public ChangeTypeDescriptionDetachedProbe(string attributeName, string add, string modify, string delete)
            {
                this.attributeName = attributeName;
                this.add = add;
                this.modify = modify;
                this.delete = delete;
            }

            public override string AttributeName => this.attributeName;

            public override string Add => this.add;

            public override string Modify => this.modify;

            public override string Delete => this.delete;
        }

        private static string Unsecure(SecureString secure)
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secure);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(ptr);
            }
        }
    }
}
