using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    /// <summary>
    /// End-to-end tests for the packaged-MA manifest BUILD WIRING (Task 5). Unlike
    /// <see cref="Lithnet.Ecma2Framework.Hosting.Tests"/>' ManifestEmitterTests (which call the emitter
    /// directly in-process), these tests drive the REAL consumer build: the framework targets scaffold/import
    /// the props, gate on the opt-in switch, validate, and run the worker exe in emit-manifest mode.
    ///
    ///   1. <see cref="OptedIn_Manifest_IsEmittedNextToWorker_WithExpectedContent"/> asserts the manifest the
    ///      SampleConsumer build produced (the SampleConsumer commits a props file with the opt-in ON, a fixed
    ///      GUID, company "Lithnet", list name "Sample"). Config parameters are NOT baked into the manifest
    ///      (omit-schema semantics): the parameter sections must be empty.
    ///   2. <see cref="NotOptedIn_OverrideBuild_ProducesNoManifest"/> rebuilds the SampleConsumer with the
    ///      opt-in overridden OFF into an isolated output path and asserts no manifest is produced and the
    ///      build still succeeds.
    /// </summary>
    [TestClass]
    public class PackagedMaManifestEndToEndTests
    {
        private const string TestManagementAgentId = "{6F9619FF-8B86-D011-B42D-00CF4FC964FF}";
        private const string TestCompany = "Lithnet";
        private const string TestListName = "Sample";

        private const string ShimAssemblyName = "Lithnet.Ecma2Framework.SampleConsumer.Ecma2";
        private const string ManifestFileName = "Lithnet.Ecma2Framework.SampleConsumer.Ecma2.PackagedMA.xml";
        private const string ShimDllFileName = "Lithnet.Ecma2Framework.SampleConsumer.Ecma2.dll";

        // Design C: the worker is the generated host exe (Ecma2Host.exe), FLAT in the consumer output ROOT (the
        // consumer output + the host form one net8 worker). The net48 shim DLL + the manifest sit in the
        // 'ecma2\' subfolder.
        private const string ShimSubfolderName = "ecma2";
        private const string WorkerExeFileName = "Ecma2Host.exe";

#if DEBUG
        private const string Config = "Debug";
#else
        private const string Config = "Release";
#endif

        private static string ResolveSrcDir()
        {
            string testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Path: ...Tests\bin\<Config>\net48 -- four levels up reaches src\.
            return Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));
        }

        private static string ResolveSampleProjectDir()
        {
            return Path.Combine(ResolveSrcDir(), "Lithnet.Ecma2Framework.SampleConsumer");
        }

        private static string ResolveSampleOutputDir()
        {
            return Path.Combine(ResolveSampleProjectDir(), "bin", Config, "net8.0");
        }

        [TestMethod]
        public void OptedIn_Manifest_IsEmittedNextToWorker_WithExpectedContent()
        {
            string sampleOutputDir = ResolveSampleOutputDir();
            string manifestPath = Path.Combine(sampleOutputDir, ShimSubfolderName, ManifestFileName);

            Assert.IsTrue(
                Directory.Exists(sampleOutputDir),
                string.Format("SampleConsumer output directory not found at '{0}'. Build the SampleConsumer first.", sampleOutputDir));

            Assert.IsTrue(
                File.Exists(manifestPath),
                string.Format(
                    "Packaged-MA manifest '{0}' was not emitted in '{1}'. The framework targets must run the worker " +
                    "in emit-manifest mode when Ecma2GeneratePackagedMa is on.",
                    ManifestFileName,
                    sampleOutputDir));

            // The worker (generated host exe) sits FLAT in the consumer output root; the manifest + shim DLL sit
            // together in the 'ecma2\' subfolder.
            Assert.IsTrue(File.Exists(Path.Combine(sampleOutputDir, WorkerExeFileName)), "Worker exe must be present in the consumer output root.");
            Assert.IsTrue(File.Exists(Path.Combine(sampleOutputDir, ShimSubfolderName, ShimDllFileName)), "Shim DLL must be present next to the manifest in the ecma2 subfolder.");

            XDocument document = XDocument.Load(manifestPath);
            XElement maData = document.Root.Element("ma-data");
            Assert.IsNotNull(maData, "<ma-data> must be present.");

            Assert.AreEqual("Packaged", maData.Attribute("format").Value, "ma-data/@format must be Packaged.");
            Assert.AreEqual(TestManagementAgentId, maData.Element("id").Value, "<id> must equal the committed test GUID.");
            Assert.AreEqual(TestCompany, maData.Element("ma-companyname").Value, "<ma-companyname> must equal the committed company.");
            Assert.AreEqual("ecma2-framework (" + TestCompany + ")", maData.Element("subtype").Value, "<subtype> must reflect the company.");
            Assert.AreEqual(TestListName + " (" + TestCompany + ")", maData.Element("ma-listname").Value, "<ma-listname> must reflect the list name and company.");

            // A placeholder DSML <schema> is present in <ma-data>; there is no <rediscover-schema>;
            // the <capabilities-mask> IS present at the ma-data level.
            Assert.IsNotNull(maData.Element("schema"), "<schema> placeholder must be present in <ma-data>.");
            Assert.IsNull(maData.Element("rediscover-schema"), "<rediscover-schema> must NOT be at the ma-data level.");
            Assert.IsNotNull(maData.Element("capabilities-mask"), "<capabilities-mask> must be present at the ma-data level.");

            XElement uiData = maData.Element("private-configuration").Element("MAConfig").Element("ui-data");
            Assert.AreEqual("xmlwizard", uiData.Elements().First().Name.LocalName, "<xmlwizard> must be the first child of <ui-data>.");

            XElement extensionConfig = maData
                .Element("private-configuration")
                .Element("MAConfig")
                .Element("extension-config");

            Assert.AreEqual(ShimDllFileName, extensionConfig.Element("filename").Value, "<filename> must be the shim DLL file name.");

            uint expectedBits = ExpectedSampleCapabilityBits();
            Assert.AreEqual(
                expectedBits.ToString(System.Globalization.CultureInfo.InvariantCulture),
                extensionConfig.Element("capability-bits").Value,
                "<capability-bits> must equal the encoder output for the SampleCapabilitiesProvider caps.");

            // <capabilities-mask> is the same engine value as <capability-bits>, in lowercase hex.
            Assert.AreEqual(
                expectedBits.ToString("x", System.Globalization.CultureInfo.InvariantCulture),
                maData.Element("capabilities-mask").Value,
                "<capabilities-mask> must be the capability bits in lowercase hex.");

            // supports-parameters-ex must be 1 (the v3 shim implements IMAExtensible2GetParametersEx).
            Assert.AreEqual("1", extensionConfig.Element("supports-parameters-ex").Value, "<supports-parameters-ex> must be 1.");

            // Parameter sections must be EMPTY: with omit-schema, FIM rediscovers config parameters live via
            // the shim's IMAExtensible2GetParametersEx; nothing is baked into the manifest.
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

        [TestMethod]
        public void NotOptedIn_OverrideBuild_ProducesNoManifest()
        {
            string sampleProjectDir = ResolveSampleProjectDir();
            string sampleProject = Path.Combine(sampleProjectDir, "Lithnet.Ecma2Framework.SampleConsumer.csproj");

            Assert.IsTrue(File.Exists(sampleProject), "SampleConsumer project not found at: " + sampleProject);

            // Build the SampleConsumer with the opt-in overridden OFF, redirecting only the FINAL output dir
            // (OutDir) to an isolated temp folder. This keeps the build off the shared bin\ that the test host
            // has the generated shim DLL loaded from (GeneratedShimEndToEndTests does Assembly.LoadFrom on it),
            // which would otherwise lock the shim copy. The intermediate (obj) dir is left shared, so restore and
            // project-reference assets resolve normally - redirecting BaseIntermediateOutputPath instead needs its
            // own restore and is brittle. The OutDir is passed with a trailing '/' so the closing quote is not
            // escaped on the command line.
            string isolatedOut = (Path.Combine(Path.GetTempPath(), "ecma2-nooptin-" + Guid.NewGuid().ToString("N")) + Path.DirectorySeparatorChar).Replace('\\', '/');

            // -v:n surfaces the low-key, normal-importance "generation is off" message, which the default minimal
            // verbosity would suppress.
            int offExitCode = RunDotnetBuild(
                sampleProject,
                "-c " + Config + " -v:n -p:Ecma2GeneratePackagedMa=false -p:OutDir=\"" + isolatedOut + "\"",
                out string offLog);

            try
            {
                Assert.AreEqual(0, offExitCode, "The not-opted-in (OFF) build must succeed. Build log:" + Environment.NewLine + offLog);

                Assert.IsTrue(
                    offLog.IndexOf("Packaged MA generation is off", StringComparison.Ordinal) >= 0,
                    "The OFF build must surface the visible 'generation is off' skip message (not a silent skip). Build log:" + Environment.NewLine + offLog);

                Assert.IsTrue(
                    offLog.IndexOf("[manifest] wrote", StringComparison.Ordinal) < 0,
                    "The OFF build must NOT invoke the worker's emit-manifest mode. Build log:" + Environment.NewLine + offLog);

                // No *.PackagedMA.xml may be produced anywhere under the isolated output dir.
                string isolatedOutWin = isolatedOut.Replace('/', Path.DirectorySeparatorChar);
                if (Directory.Exists(isolatedOutWin))
                {
                    string[] manifests = Directory.GetFiles(isolatedOutWin, "*.PackagedMA.xml", SearchOption.AllDirectories);
                    Assert.AreEqual(
                        0,
                        manifests.Length,
                        "No packaged-MA manifest may be produced when Ecma2GeneratePackagedMa is off. Found: " + string.Join(", ", manifests));
                }
            }
            finally
            {
                string isolatedOutWin = isolatedOut.Replace('/', Path.DirectorySeparatorChar);

                try
                {
                    if (Directory.Exists(isolatedOutWin))
                    {
                        Directory.Delete(isolatedOutWin, true);
                    }
                }
                catch (IOException)
                {
                    // Best-effort cleanup of the temp output; a locked file must not fail the test.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static int RunDotnetBuild(string project, string extraArgs, out string log)
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build \"" + project + "\" " + extraArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            System.Text.StringBuilder output = new System.Text.StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = psi;
                process.OutputDataReceived += (s, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };
                process.ErrorDataReceived += (s, e) => { if (e.Data != null) { lock (output) { output.AppendLine(e.Data); } } };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                log = output.ToString();
                return process.ExitCode;
            }
        }

        /// <summary>
        /// Computes the expected <c>capability-bits</c> for the SampleCapabilitiesProvider's capabilities
        /// (SupportImport=true; export, password, hierarchy, partitions all off; DN style None; ConcurrentOperation
        /// and DeleteAddAsReplace both off; default export type).
        /// This mirrors <c>Lithnet.Ecma2Framework.Hosting.Manifest.CapabilityEncoder</c>, which lives in the
        /// net8 Worker assembly and is internal, so it cannot be referenced from this net48 test. The constants
        /// below are the SAME ground-truth bit encoding; keep them in lockstep with the encoder.
        /// Lockstep note: the encoder now maps ConcurrentOperation -> ConcurrentExecution (0x8000) and
        /// DeleteAddAsReplace -> FullReplaceOnDelete (0x10000, a single bit). The SampleCapabilitiesProvider
        /// leaves both off, so neither contributes here and the expected value is unchanged; if the sample ever
        /// turns either on, add the corresponding bit below to stay in lockstep.
        /// </summary>
        private static uint ExpectedSampleCapabilityBits()
        {
            const uint Baseline = 0x20000 | 0x40000 | 0x80000000;
            const uint NoHierarchy = 0x8;
            const uint NoExport = 0x80;
            const uint NoPartitions = 0x1000000;

            uint bits = Baseline;

            // SupportImport = true => NoImport (0x4) NOT set.
            // SupportHierarchy = false => NoHierarchy set.
            bits |= NoHierarchy;
            // SupportExport = false => NoExport set.
            bits |= NoExport;
            // SupportPartitions = false => NoPartitions set.
            bits |= NoPartitions;

            // ConcurrentOperation = false => ConcurrentExecution (0x8000) NOT set.
            // DeleteAddAsReplace = false => FullReplaceOnDelete (0x10000) NOT set.

            return bits;
        }
    }
}
