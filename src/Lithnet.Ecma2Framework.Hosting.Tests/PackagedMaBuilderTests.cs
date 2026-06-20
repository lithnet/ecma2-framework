using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Lithnet.Ecma2Framework;
using Lithnet.Ecma2Framework.Hosting.Manifest;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    [TestClass]
    public class PackagedMaBuilderTests
    {
        // ----- The all-params probe MA whose mapackager output is the golden reference. -----
        private const string ProbeGuid = "{FA22382E-86BC-4D52-8517-929E5F48AB76}";
        private const string ProbeCompany = "Lithnet";
        private const string ProbeListName = "Probe";
        private const string ProbeHelpText = "All params probe";
        private const string ProbeShimFileName = "Lithnet.Ecma2.AllParamsProbe.dll";
        private const string ProbeAssemblyVersion = "1.0.0.0";

        // The probe's live capability_mask = 0x810780B8 (decimal 2164752568), export-type = 2. The probe is
        // import-only: export and password unsupported.
        private const uint ProbeCapabilityBits = 0x810780B8u;
        private const int ProbeExportType = 2;

        // ----- A second identity used by the shape tests (export- and password-capable). -----
        private const string OktaCapabilityBits = "2164758584";
        // Export-capable, so the mask carries the mask-only ImmediateExportConfirmation bit:
        // 0x81079838 (bits) | 0x1 = 0x81079839 = 2164758585.
        private const uint OktaCapabilityMask = 2164758585u;
        private const string OktaCapabilityMaskHex = "81079839";
        private const string OktaExportType = "5";
        private const string OktaGuid = "{B965F673-D98D-4A77-9708-3CD4F63AB4C8}";
        private const string ShimFileName = "Lithnet.Okta.ManagementAgent.Ecma2.dll";
        private const string AssemblyVersion = "1.0.0.0";

        // Page sizes the builder emits, mirroring the v3 shim's connection values (100 / 10000). These are a
        // legitimate difference from the probe golden (whose probe reported 100/1000 import, 1/1 export).
        private const string ExpectedImportDefaultPageSize = "100";
        private const string ExpectedImportMaxPageSize = "10000";
        private const string ExpectedExportDefaultPageSize = "100";
        private const string ExpectedExportMaxPageSize = "10000";

        private static ManifestIdentity BuildIdentity()
        {
            return new ManifestIdentity
            {
                Company = "Lithnet",
                ListName = "Okta",
                HelpText = "Lithnet Okta Management Agent",
                ManagementAgentId = OktaGuid,
                InternalVersion = 1,
            };
        }

        private static MACapabilities BuildCapabilities()
        {
            return new MACapabilities
            {
                SupportImport = true,
                SupportExport = true,
                SupportPassword = true,
            };
        }

        private static XDocument BuildDocument()
        {
            string xml = PackagedMaBuilder.Build(
                BuildIdentity(),
                2164758584u,
                OktaCapabilityMask,
                5,
                ShimFileName,
                AssemblyVersion,
                BuildCapabilities());

            return XDocument.Parse(xml);
        }

        // -------------------------------------------------------------------------------------------------
        // Golden structural test: the builder's output for the probe's inputs must match the real
        // mapackager manifest element-by-element, except for documented legitimate differences.
        // -------------------------------------------------------------------------------------------------

        [TestMethod]
        public void Build_MatchesMapackagerGolden_ElementByElement()
        {
            XDocument golden = LoadGolden();

            MACapabilities probeCaps = new MACapabilities
            {
                // The probe is import-only: export and password are unsupported. SupportImport is on so the
                // import-only golden's <import-enabled>1</import-enabled> matches.
                SupportImport = true,
                SupportExport = false,
                SupportPassword = false,
            };

            string xml = PackagedMaBuilder.Build(
                new ManifestIdentity
                {
                    Company = ProbeCompany,
                    ListName = ProbeListName,
                    HelpText = ProbeHelpText,
                    ManagementAgentId = ProbeGuid,
                    InternalVersion = 1,
                },
                ProbeCapabilityBits,
                // Import-only probe: no export, so the mask equals the bits (matches the golden).
                ProbeCapabilityBits,
                ProbeExportType,
                ProbeShimFileName,
                ProbeAssemblyVersion,
                probeCaps);

            XDocument actual = XDocument.Parse(xml);

            // Documented legitimate differences between our schema-less generation and the probe export:
            //   - page sizes: ours mirror the shim (100/10000), the probe reported 100/1000 and 1/1.
            //   - default_visible_attributes: the probe carried id/displayName from its export schema; we
            //     bake no schema, so ours is empty.
            // Both are asserted explicitly below after the structural compare passes.
            HashSet<string> valueExceptions = new HashSet<string>
            {
                "export-ma/ma-data/private-configuration/MAConfig/extension-config/import-default-page-size",
                "export-ma/ma-data/private-configuration/MAConfig/extension-config/import-max-page-size",
                "export-ma/ma-data/private-configuration/MAConfig/extension-config/export-default-page-size",
                "export-ma/ma-data/private-configuration/MAConfig/extension-config/export-max-page-size",
            };

            // childCountExceptions: paths where the child-set legitimately differs (default_visible_attributes
            // has <attribute> children in the golden, none in ours).
            HashSet<string> childCountExceptions = new HashSet<string>
            {
                "export-ma/ma-data/private-configuration/MAConfig/default_visible_attributes",
            };

            List<string> differences = new List<string>();
            CompareElements(golden.Root, actual.Root, "export-ma", valueExceptions, childCountExceptions, differences);

            Assert.AreEqual(
                0,
                differences.Count,
                "Builder output diverges from the mapackager golden:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, differences));

            // Now assert the documented legitimate differences explicitly.
            XElement extensionConfig = actual.Descendants("extension-config").Single();
            Assert.AreEqual(ExpectedImportDefaultPageSize, (string)extensionConfig.Element("import-default-page-size"), "Import default page size must mirror the shim (100).");
            Assert.AreEqual(ExpectedImportMaxPageSize, (string)extensionConfig.Element("import-max-page-size"), "Import max page size must mirror the shim (10000).");
            Assert.AreEqual(ExpectedExportDefaultPageSize, (string)extensionConfig.Element("export-default-page-size"), "Export default page size must mirror the shim (100).");
            Assert.AreEqual(ExpectedExportMaxPageSize, (string)extensionConfig.Element("export-max-page-size"), "Export max page size must mirror the shim (10000).");

            XElement defaultVisible = actual.Descendants("default_visible_attributes").Single();
            Assert.IsFalse(defaultVisible.Elements().Any(), "default_visible_attributes must be empty for our schema-less generation.");
        }

        /// <summary>
        /// Recursively compares the structure (element names, ordering, attributes) and text values of two
        /// XML trees. Differences are appended to <paramref name="differences"/> rather than asserted, so the
        /// caller can report all of them at once. <paramref name="valueExceptions"/> names element paths whose
        /// text value may legitimately differ (structure is still compared); <paramref name="childCountExceptions"/>
        /// names paths whose child set may legitimately differ (children are not recursed).
        /// </summary>
        private static void CompareElements(XElement expected, XElement actual, string path, HashSet<string> valueExceptions, HashSet<string> childCountExceptions, List<string> differences)
        {
            if (expected.Name != actual.Name)
            {
                differences.Add(string.Format("{0}: element name mismatch (expected '{1}', actual '{2}').", path, expected.Name, actual.Name));
                return;
            }

            // Compare attributes (name + value), order-independent.
            Dictionary<string, string> expectedAttrs = expected.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value);
            Dictionary<string, string> actualAttrs = actual.Attributes().ToDictionary(a => a.Name.ToString(), a => a.Value);

            foreach (KeyValuePair<string, string> attr in expectedAttrs)
            {
                if (!actualAttrs.ContainsKey(attr.Key))
                {
                    differences.Add(string.Format("{0}: missing attribute '{1}'.", path, attr.Key));
                }
                else if (actualAttrs[attr.Key] != attr.Value)
                {
                    differences.Add(string.Format("{0}@{1}: value mismatch (expected '{2}', actual '{3}').", path, attr.Key, attr.Value, actualAttrs[attr.Key]));
                }
            }

            foreach (string actualAttrName in actualAttrs.Keys)
            {
                if (!expectedAttrs.ContainsKey(actualAttrName))
                {
                    differences.Add(string.Format("{0}: unexpected attribute '{1}'.", path, actualAttrName));
                }
            }

            List<XElement> expectedChildren = expected.Elements().ToList();
            List<XElement> actualChildren = actual.Elements().ToList();

            if (childCountExceptions.Contains(path))
            {
                // Child set legitimately differs; do not compare or recurse children for this node.
                return;
            }

            if (expectedChildren.Count == 0 && actualChildren.Count == 0)
            {
                // Leaf node: compare text value unless this path is a documented value exception.
                if (!valueExceptions.Contains(path))
                {
                    string expectedValue = expected.Value.Trim();
                    string actualValue = actual.Value.Trim();

                    if (expectedValue != actualValue)
                    {
                        differences.Add(string.Format("{0}: text value mismatch (expected '{1}', actual '{2}').", path, expectedValue, actualValue));
                    }
                }

                return;
            }

            if (expectedChildren.Count != actualChildren.Count)
            {
                differences.Add(string.Format(
                    "{0}: child count mismatch (expected {1} [{2}], actual {3} [{4}]).",
                    path,
                    expectedChildren.Count,
                    string.Join(",", expectedChildren.Select(e => e.Name.LocalName)),
                    actualChildren.Count,
                    string.Join(",", actualChildren.Select(e => e.Name.LocalName))));
                return;
            }

            for (int i = 0; i < expectedChildren.Count; i++)
            {
                string childPath = path + "/" + expectedChildren[i].Name.LocalName;
                CompareElements(expectedChildren[i], actualChildren[i], childPath, valueExceptions, childCountExceptions, differences);
            }
        }

        private static XDocument LoadGolden()
        {
            string testDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string goldenPath = Path.Combine(testDir, "Manifest", "packaged.golden.xml");
            Assert.IsTrue(File.Exists(goldenPath), "Golden manifest not found at: " + goldenPath);
            return XDocument.Load(goldenPath);
        }

        // -------------------------------------------------------------------------------------------------
        // Shape tests.
        // -------------------------------------------------------------------------------------------------

        [TestMethod]
        public void Build_RootIsExportMa_WithPackagedMaData()
        {
            XDocument doc = BuildDocument();

            Assert.AreEqual("export-ma", doc.Root.Name.LocalName);

            XElement maData = doc.Root.Element("ma-data");
            Assert.IsNotNull(maData);
            Assert.AreEqual("Packaged", (string)maData.Attribute("format"));
        }

        [TestMethod]
        public void Build_IdentityValues_AreStamped()
        {
            XElement maData = BuildDocument().Root.Element("ma-data");

            Assert.AreEqual("ecma2-framework (Lithnet)", (string)maData.Element("subtype"));
            Assert.AreEqual("Okta (Lithnet)", (string)maData.Element("ma-listname"));
            Assert.AreEqual(OktaGuid, (string)maData.Element("id"));
            Assert.AreEqual("Extensible2", (string)maData.Element("category"));
            Assert.AreEqual("Lithnet", (string)maData.Element("ma-companyname"));
            Assert.AreEqual("1", (string)maData.Element("internal-version"));
        }

        [TestMethod]
        public void Build_PlaceholderSchema_IsInMaData_NoRediscover()
        {
            XDocument doc = BuildDocument();

            // MIM's packaged-MA import REQUIRES a DSML <schema> in <ma-data>. It is a minimal PLACEHOLDER
            // (one structural class + one anchor attribute); the real per-tenant schema is dynamic and is
            // discovered by a schema-refresh after the MA is configured. There is no <rediscover-schema> anywhere.
            XElement maData = doc.Root.Element("ma-data");

            XElement schema = maData.Element("schema");
            Assert.IsNotNull(schema, "<schema> must be present in <ma-data>.");
            Assert.IsFalse(doc.Descendants("rediscover-schema").Any(), "<rediscover-schema> must NOT be emitted anywhere.");

            XNamespace dsml = "http://www.dsml.org/DSML";
            XElement placeholderClass = schema.Element(dsml + "dsml").Element(dsml + "directory-schema").Element(dsml + "class");
            Assert.AreEqual("placeholder", (string)placeholderClass.Attribute("id"), "the placeholder class must be present.");

            // <ui-data>'s first child is the import-wizard config; the schema lives in <ma-data>, not here.
            XElement uiData = maData
                .Element("private-configuration")
                .Element("MAConfig")
                .Element("ui-data");

            Assert.AreEqual("xmlwizard", uiData.Elements().First().Name.LocalName, "<xmlwizard> must be the first child of <ui-data>.");
        }

        [TestMethod]
        public void Build_CapabilitiesMask_IsLowercaseHexAtMaDataLevel()
        {
            XDocument doc = BuildDocument();

            XElement maData = doc.Root.Element("ma-data");
            XElement mask = maData.Element("capabilities-mask");

            Assert.IsNotNull(mask, "<capabilities-mask> must be present at the ma-data level.");

            // The builder renders whatever mask it is given, in lowercase hex with no 0x prefix. This
            // identity is export-capable, so the caller's mask carries ImmediateExportConfirmation:
            // 0x81079838 | 0x1 = 0x81079839.
            Assert.AreEqual(OktaCapabilityMaskHex, mask.Value, "<capabilities-mask> must be the supplied mask in lowercase hex with no 0x prefix.");

            // <capability-bits> (extension-config) stays WITHOUT the mask-only bit.
            XElement extensionConfig = doc.Descendants("extension-config").Single();
            Assert.AreEqual(OktaCapabilityBits, (string)extensionConfig.Element("capability-bits"));
        }

        [TestMethod]
        public void Build_ParameterSections_AreEmpty()
        {
            XDocument doc = BuildDocument();

            XElement parameterDefinitions = doc.Descendants("parameter-definitions").Single();
            Assert.IsFalse(parameterDefinitions.Elements().Any(), "<parameter-definitions> must be emitted empty (no baked parameter rows).");
            Assert.IsFalse(parameterDefinitions.Attributes().Any(), "<parameter-definitions> must carry no refresh* attributes when empty.");

            XElement parameterValues = doc.Descendants("parameter-values").Single();
            Assert.IsFalse(parameterValues.Elements().Any(), "<parameter-values> must be emitted empty.");
        }

        [TestMethod]
        public void Build_SupportsParametersEx_IsOne()
        {
            XElement extensionConfig = BuildDocument().Descendants("extension-config").Single();
            Assert.AreEqual("1", (string)extensionConfig.Element("supports-parameters-ex"), "<supports-parameters-ex> must be 1 (the v3 shim implements IMAExtensible2GetParametersEx).");
        }

        [TestMethod]
        public void Build_ExtensionConfig_ContainsExpectedValues()
        {
            XElement extensionConfig = BuildDocument().Descendants("extension-config").Single();

            XElement filename = extensionConfig.Element("filename");
            Assert.AreEqual(ShimFileName, (string)filename);
            Assert.AreEqual("ISV", (string)filename.Attribute("data-owner"));

            Assert.AreEqual(OktaCapabilityBits, (string)extensionConfig.Element("capability-bits"));
            Assert.AreEqual(OktaExportType, (string)extensionConfig.Element("export-type"));
            Assert.AreEqual(AssemblyVersion, (string)extensionConfig.Element("assembly-version"));
            Assert.AreEqual("1", (string)extensionConfig.Element("import-enabled"));
            Assert.AreEqual("1", (string)extensionConfig.Element("export-enabled"));

            // Export- and password-capable identity: export-mode is call-based, password-management-enabled is 1.
            Assert.AreEqual("call-based", (string)extensionConfig.Element("export-mode"));
            Assert.AreEqual("call-based", (string)extensionConfig.Element("import-mode"));
            Assert.AreEqual("1", (string)extensionConfig.Element("password-management-enabled"));

            // Page sizes mirror the shim.
            Assert.AreEqual(ExpectedImportDefaultPageSize, (string)extensionConfig.Element("import-default-page-size"));
            Assert.AreEqual(ExpectedImportMaxPageSize, (string)extensionConfig.Element("import-max-page-size"));
            Assert.AreEqual(ExpectedExportDefaultPageSize, (string)extensionConfig.Element("export-default-page-size"));
            Assert.AreEqual(ExpectedExportMaxPageSize, (string)extensionConfig.Element("export-max-page-size"));
        }

        [TestMethod]
        public void Build_ImportOnlyCapabilities_EmitEmptyExportModeAndPassword()
        {
            MACapabilities caps = new MACapabilities
            {
                SupportImport = true,
                SupportExport = false,
                SupportPassword = false,
            };

            string xml = PackagedMaBuilder.Build(
                BuildIdentity(),
                ProbeCapabilityBits,
                ProbeCapabilityBits,
                ProbeExportType,
                ShimFileName,
                AssemblyVersion,
                caps);

            XElement extensionConfig = XDocument.Parse(xml).Descendants("extension-config").Single();

            Assert.AreEqual("1", (string)extensionConfig.Element("import-enabled"));
            Assert.AreEqual("0", (string)extensionConfig.Element("export-enabled"));

            // export-mode and password-management-enabled are EMPTY when export/password are unsupported.
            Assert.AreEqual(string.Empty, (string)extensionConfig.Element("export-mode"));
            Assert.AreEqual(string.Empty, (string)extensionConfig.Element("password-management-enabled"));

            // import-mode stays call-based regardless.
            Assert.AreEqual("call-based", (string)extensionConfig.Element("import-mode"));
        }

        [TestMethod]
        public void Build_DefaultVisibleAttributes_IsEmpty()
        {
            XElement maData = BuildDocument().Descendants("default_visible_attributes").Single();

            Assert.IsFalse(maData.Elements().Any(), "default_visible_attributes must be emitted empty.");
        }

        [TestMethod]
        public void Build_OutputIsValidXml()
        {
            string xml = PackagedMaBuilder.Build(
                BuildIdentity(),
                2164758584u,
                OktaCapabilityMask,
                5,
                ShimFileName,
                AssemblyVersion,
                BuildCapabilities());

            // Parsing throws on invalid XML; a non-throwing parse is the assertion.
            XDocument doc = XDocument.Parse(xml);
            Assert.IsNotNull(doc.Root);
        }
    }
}
