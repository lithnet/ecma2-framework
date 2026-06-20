using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Lithnet.Ecma2Framework.Hosting;
using Lithnet.Ecma2Framework.Hosting.Manifest;
using Lithnet.Ecma2Framework.TestConsumer;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// End-to-end tests for <see cref="ManifestEmitter"/>. These boot the real TestConsumer DI
    /// container (no live connection), read its real <see cref="TestCapabilitiesProvider"/>, and assert
    /// the emitted Packaged-MA manifest. Config parameters are NOT baked into the manifest (omit-schema
    /// semantics): FIM rediscovers them live via the shim's IMAExtensible2GetParametersEx, so the
    /// manifest's parameter sections must be empty.
    /// </summary>
    [TestClass]
    public sealed class ManifestEmitterTests
    {
        private const string TestGuid = "{3F2504E0-4F89-41D3-9A0C-0305E82C3301}";
        private const string TestCompany = "Lithnet";
        private const string TestListName = "Test MA";
        private const string TestHelpText = "Test help text";
        private const string TestShimFileName = "Lithnet.Ecma2Framework.TestConsumer.Ecma2.dll";
        private const string TestAssemblyVersion = "1.2.3.4";

        private static WorkerHost LoadHost()
        {
            return WorkerHost.Create(new TestConsumerStartup(), new DefaultConfigRegistrationProvider());
        }

        private static ManifestEmitOptions BuildOptions(string outputPath)
        {
            return new ManifestEmitOptions
            {
                OutputPath = outputPath,
                ManagementAgentId = TestGuid,
                Company = TestCompany,
                ListName = TestListName,
                HelpText = TestHelpText,
                ShimFileName = TestShimFileName,
                AssemblyVersion = TestAssemblyVersion,
            };
        }

        [TestMethod]
        public async Task Emit_WritesManifest_WithExpectedIdentityAndCapabilities()
        {
            WorkerHost host = LoadHost();
            string outputPath = Path.Combine(Path.GetTempPath(), "ecma2-manifest-" + Path.GetRandomFileName() + ".xml");

            try
            {
                await ManifestEmitter.EmitAsync(host, BuildOptions(outputPath));

                Assert.IsTrue(File.Exists(outputPath), "Manifest file was not written.");

                XDocument document = XDocument.Load(outputPath);
                XElement maData = document.Root.Element("ma-data");
                Assert.IsNotNull(maData, "ma-data element must be present.");

                // Identity assertions.
                Assert.AreEqual(TestGuid, maData.Element("id").Value, "<id> must reflect the supplied GUID.");
                Assert.AreEqual(TestCompany, maData.Element("ma-companyname").Value, "<ma-companyname> must reflect the company.");
                Assert.AreEqual("ecma2-framework (" + TestCompany + ")", maData.Element("subtype").Value, "<subtype> must reflect the company.");
                Assert.AreEqual(TestListName + " (" + TestCompany + ")", maData.Element("ma-listname").Value, "<ma-listname> must reflect the list name and company.");

                // The capability-bits value must equal the encoder's output for the TestConsumer's caps.
                MACapabilities expectedCaps = new MACapabilities
                {
                    SupportImport = true,
                    SupportExport = true,
                    SupportPassword = true,
                };
                uint expectedBits = CapabilityEncoder.GetCapabilityBits(expectedCaps);
                uint expectedMask = CapabilityEncoder.GetCapabilitiesMask(expectedCaps);

                XElement extensionConfig = maData
                    .Element("private-configuration")
                    .Element("MAConfig")
                    .Element("extension-config");

                Assert.AreEqual(
                    expectedBits.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    extensionConfig.Element("capability-bits").Value,
                    "<capability-bits> must equal CapabilityEncoder.GetCapabilityBits for the provider's caps.");

                // <capabilities-mask> is GetCapabilitiesMask (bits + the export-only ImmediateExportConfirmation
                // bit; the TestConsumer supports export), in lowercase hex.
                Assert.AreEqual(
                    expectedMask.ToString("x", System.Globalization.CultureInfo.InvariantCulture),
                    maData.Element("capabilities-mask").Value,
                    "<capabilities-mask> must be CapabilityEncoder.GetCapabilitiesMask in lowercase hex.");

                Assert.AreEqual(TestShimFileName, extensionConfig.Element("filename").Value, "<filename> must reflect the shim file name.");
                Assert.AreEqual(TestAssemblyVersion, extensionConfig.Element("assembly-version").Value, "<assembly-version> must reflect the supplied version.");

                // supports-parameters-ex must be 1 (the v3 shim implements IMAExtensible2GetParametersEx).
                Assert.AreEqual("1", extensionConfig.Element("supports-parameters-ex").Value, "<supports-parameters-ex> must be 1.");

                // A placeholder DSML <schema> is present in <ma-data>; there is no <rediscover-schema>; capabilities-mask is present.
                Assert.IsNotNull(maData.Element("schema"), "<schema> placeholder must be present in <ma-data>.");
                Assert.IsNull(maData.Element("rediscover-schema"), "<rediscover-schema> must NOT be at the ma-data level.");
                Assert.IsNotNull(maData.Element("capabilities-mask"), "<capabilities-mask> must be present at the ma-data level.");

                XElement uiData = maData.Element("private-configuration").Element("MAConfig").Element("ui-data");
                Assert.AreEqual("xmlwizard", uiData.Elements().First().Name.LocalName, "<xmlwizard> must be the first child of <ui-data>.");

                // Parameter sections must be EMPTY: FIM rediscovers config parameters live; nothing is baked.
                XElement parameterDefinitions = maData
                    .Element("private-configuration")
                    .Element("MAConfig")
                    .Element("parameter-definitions");
                Assert.IsFalse(parameterDefinitions.Elements().Any(), "<parameter-definitions> must be empty (no baked parameter rows).");

                XElement parameterValues = maData
                    .Element("private-configuration")
                    .Element("MAConfig")
                    .Element("parameter-values");
                Assert.IsFalse(parameterValues.Elements().Any(), "<parameter-values> must be empty.");
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [TestMethod]
        public async Task Emit_BlankManagementAgentId_ThrowsAndWritesNoFile()
        {
            WorkerHost host = LoadHost();
            string outputPath = Path.Combine(Path.GetTempPath(), "ecma2-manifest-" + Path.GetRandomFileName() + ".xml");

            ManifestEmitOptions options = BuildOptions(outputPath);
            options.ManagementAgentId = "   ";

            try
            {
                await Assert.ThrowsAsync<System.ArgumentException>(() => ManifestEmitter.EmitAsync(host, options),
                    "A blank ManagementAgentId must throw.");

                Assert.IsFalse(File.Exists(outputPath), "No manifest file must be written when a required option is blank.");
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }
    }
}
