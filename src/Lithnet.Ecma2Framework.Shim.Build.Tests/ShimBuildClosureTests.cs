using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Shim.Build.Tests
{
    /// <summary>
    /// Part C: the real net48 compile-proof of the two-output build and of Task 4a's generated wrappers.
    ///
    /// The class builds the shim once (it is the expensive step) for a sample MA name via the real build
    /// mechanism, then reads the produced assembly's metadata and asserts the four invariants:
    ///   1. the assembly's simple name is the MA name,
    ///   2. its public exported types are EXACTLY the four host-facing impls (nothing internal leaks; the
    ///      impls are fixed-named shared source in the Lithnet.Ecma2Framework.Shim namespace - safe because
    ///      each shim is its own assembly and FIM resolves the extension type from the named assembly),
    ///   3. each impl declares its expected IMAExtensible2* interface, and
    ///   4. its referenced assemblies contain no Lithnet.* (the closure proof).
    ///
    /// A build failure here means a generated template signature is wrong against the real net48 host
    /// surface - a genuine finding, not something to paper over.
    /// </summary>
    [TestClass]
    public class ShimBuildClosureTests
    {
        private const string SampleManagementAgentName = "Test.Shim.MA";

        // The four host-facing impls are fixed-named shared source in this namespace (no longer per-MA
        // generated). The shim ASSEMBLY is still named after the MA; only the type namespace is fixed.
        private const string ImplementationNamespace = "Lithnet.Ecma2Framework.Shim";

        private const string Configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif

        private static ShimAssemblyMetadata builtShimMetadata;

        private static string outputRoot;

        [ClassInitialize]
        public static void BuildSampleShim(TestContext context)
        {
            outputRoot = Path.Combine(Path.GetTempPath(), "ecma2-shim-build-tests", Guid.NewGuid().ToString("N"));

            ShimBuildRunner runner = new ShimBuildRunner();
            string shimAssemblyPath = runner.BuildShim(SampleManagementAgentName, Configuration, outputRoot);

            builtShimMetadata = ShimMetadataReader.Read(shimAssemblyPath);
        }

        [ClassCleanup]
        public static void RemoveBuildOutput()
        {
            if (!string.IsNullOrEmpty(outputRoot) && Directory.Exists(outputRoot))
            {
                try
                {
                    Directory.Delete(outputRoot, true);
                }
                catch (IOException)
                {
                    // Best-effort cleanup of a temp directory. A locked file here is not a test failure;
                    // the OS reclaims the temp directory eventually.
                }
            }
        }

        [TestMethod]
        public void ShimAssembly_SimpleName_IsTheManagementAgentName()
        {
            Assert.AreEqual(SampleManagementAgentName, builtShimMetadata.AssemblyName, "The shim assembly's simple name must equal the management agent name.");
        }

        [TestMethod]
        public void ShimAssembly_PublicExportedTypes_AreExactlyTheFourImplementations()
        {
            string[] expected = new[]
            {
                ImplementationNamespace + ".Ecma2Implementation",
                ImplementationNamespace + ".Ecma2ImportImplementation",
                ImplementationNamespace + ".Ecma2ExportImplementation",
                ImplementationNamespace + ".Ecma2PasswordImplementation",
            };

            List<string> actual = builtShimMetadata.PublicExportedTypes.Select(t => t.FullName).OrderBy(n => n, StringComparer.Ordinal).ToList();
            List<string> expectedSorted = expected.OrderBy(n => n, StringComparer.Ordinal).ToList();

            CollectionAssert.AreEqual(
                expectedSorted,
                actual,
                "The shim's public exported types must be EXACTLY the four host-facing impls. Found: " + string.Join(", ", actual));
        }

        [TestMethod]
        public void ShimImplementations_DeclareTheirExpectedHostInterfaces()
        {
            AssertTypeDeclaresInterface("Ecma2ImportImplementation", "Microsoft.MetadirectoryServices.IMAExtensible2CallImport");
            AssertTypeDeclaresInterface("Ecma2ExportImplementation", "Microsoft.MetadirectoryServices.IMAExtensible2CallExport");
            AssertTypeDeclaresInterface("Ecma2PasswordImplementation", "Microsoft.MetadirectoryServices.IMAExtensible2Password");

            // Ecma2Implementation services the config-UI surfaces: schema, capabilities, and both the Ex and
            // non-Ex parameters interfaces.
            AssertTypeDeclaresInterface("Ecma2Implementation", "Microsoft.MetadirectoryServices.IMAExtensible2GetSchema");
            AssertTypeDeclaresInterface("Ecma2Implementation", "Microsoft.MetadirectoryServices.IMAExtensible2GetCapabilitiesEx");
            AssertTypeDeclaresInterface("Ecma2Implementation", "Microsoft.MetadirectoryServices.IMAExtensible2GetParametersEx");
            AssertTypeDeclaresInterface("Ecma2Implementation", "Microsoft.MetadirectoryServices.IMAExtensible2GetParameters");
        }

        [TestMethod]
        public void ShimAssembly_ReferencesNoLithnetAssembly()
        {
            List<string> lithnetReferences = builtShimMetadata.ReferencedAssemblyNames
                .Where(n => n.StartsWith("Lithnet.", StringComparison.OrdinalIgnoreCase) || n.Equals("Lithnet", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.AreEqual(
                0,
                lithnetReferences.Count,
                "The shim must reference NO Lithnet.* assembly (closure proof). Found: " + string.Join(", ", lithnetReferences) + ". All references: " + string.Join(", ", builtShimMetadata.ReferencedAssemblyNames));
        }

        [TestMethod]
        public void ShimAssembly_ReferencesTheHostMmsAssembly()
        {
            // The shim's only non-BCL reference is the host MMS assembly - this confirms the closure is the
            // host surface, not a Lithnet runtime DLL.
            bool referencesMms = builtShimMetadata.ReferencedAssemblyNames.Any(n => n.Equals("Microsoft.MetadirectoryServicesEx", StringComparison.OrdinalIgnoreCase));

            Assert.IsTrue(referencesMms, "The shim must reference the host Microsoft.MetadirectoryServicesEx assembly. References: " + string.Join(", ", builtShimMetadata.ReferencedAssemblyNames));
        }

        private static void AssertTypeDeclaresInterface(string simpleTypeName, string expectedInterfaceFullName)
        {
            string fullTypeName = ImplementationNamespace + "." + simpleTypeName;

            ShimExportedType type = builtShimMetadata.PublicExportedTypes.FirstOrDefault(t => t.FullName == fullTypeName);

            Assert.IsNotNull(type, "Expected public type '" + fullTypeName + "' was not found in the shim assembly.");

            bool declaresInterface = type.ImplementedInterfaceNames.Contains(expectedInterfaceFullName);

            Assert.IsTrue(
                declaresInterface,
                "Type '" + fullTypeName + "' must declare interface '" + expectedInterfaceFullName + "'. Declared interfaces: " + string.Join(", ", type.ImplementedInterfaceNames));
        }
    }
}
