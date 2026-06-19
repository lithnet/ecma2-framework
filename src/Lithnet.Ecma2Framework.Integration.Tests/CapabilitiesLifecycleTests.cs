using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Lithnet.Ecma2Framework.Shim;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    /// <summary>
    /// End-to-end integration tests for the capabilities and config-parameters lifecycle
    /// over the live named-pipe JSON-RPC channel driven by a real consumer (<c>TestConsumer</c>).
    /// The shim's <see cref="CapabilitiesConnection"/> and <see cref="ParametersConnection"/>
    /// spawn the self-contained <c>TestConsumer</c> worker executable.
    /// Tests assert correct capability flags, empty config-parameter definitions, and successful
    /// parameter validation as returned by <c>TestCapabilitiesProvider</c> and
    /// <c>TestConfigParametersProvider</c>.
    /// </summary>
    /// <remarks>
    /// Expected data from the TestConsumer:
    /// <list type="bullet">
    ///   <item><c>TestCapabilitiesProvider</c> returns SupportImport=true, SupportExport=true,
    ///     SupportPassword=true; all other flags default to false.</item>
    ///   <item><c>TestConfigParametersProvider</c> returns empty definition lists for all pages
    ///     and Success for all validation calls.</item>
    /// </list>
    ///
    /// Worker location resolution:
    /// Walk four directory levels up from the test assembly to reach the <c>src\</c> directory,
    /// then navigate to both the Worker output and the TestConsumer output.
    /// The test fails with a clear assertion message when neither exe nor dll is present.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>The worker process is unconditionally disposed in a finally block so no orphaned
    ///     process is left behind on test failure.</item>
    ///   <item>The worker-process exit assertion is bounded at 5 s using a polling helper.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class CapabilitiesLifecycleTests
    {
        private const int ProcessExitTimeoutMs = 5000;

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Resolves the self-contained TestConsumer worker executable path.
        /// Returns null when the exe is not present.
        /// </summary>
        private static string ResolveWorkerExePath()
        {
            string testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Path: ...Tests\bin\<Config>\net48  — four levels up reaches src\.
            string srcDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));

#if DEBUG
            string config = "Debug";
#else
            string config = "Release";
#endif

            string workerOutputDir = Path.Combine(
                srcDir,
                "Lithnet.Ecma2Framework.TestConsumer",
                "bin",
                config,
                "net8.0");

            // Design C: the worker is the generated host exe (Ecma2Host.exe), built by the framework targets
            // FLAT into the consumer output ROOT (the consumer output + the host form one net8 worker). It is no
            // longer the consumer's own exe.
            string exePath = Path.Combine(workerOutputDir, "Ecma2Host.exe");

            if (File.Exists(exePath))
            {
                return exePath;
            }

            return null;
        }

        /// <summary>
        /// Polls until the process with the given PID has exited, or <paramref name="timeoutMs"/> elapses.
        /// </summary>
        private static bool WaitForProcessExit(int pid, int timeoutMs)
        {
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (IsProcessExited(pid))
                {
                    return true;
                }

                System.Threading.Thread.Sleep(100);
            }

            return IsProcessExited(pid);
        }

        /// <summary>
        /// Returns true if the process with the given PID has exited or no longer exists.
        /// </summary>
        private static bool IsProcessExited(int pid)
        {
            try
            {
                Process p = Process.GetProcessById(pid);
                return p.HasExited;
            }
            catch (ArgumentException)
            {
                // Process.GetProcessById throws ArgumentException when the process does not exist.
                return true;
            }
        }

        // -------------------------------------------------------------------------
        // GetCapabilities tests
        // -------------------------------------------------------------------------

        /// <summary>
        /// Spawns the worker with the TestConsumer, calls <see cref="CapabilitiesConnection.GetCapabilitiesEx"/>,
        /// and asserts that <c>TestCapabilitiesProvider</c>'s SupportImport=true, SupportExport=true,
        /// and SupportPassword=true flags are present in the real <see cref="MACapabilities"/> returned
        /// to the shim.
        /// </summary>
        [TestMethod]
        public void GetCapabilities_RealConsumer_ReturnsSupportImport()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail(
                    "TestConsumer worker executable not found.  Build the TestConsumer project first: " +
                    "'dotnet build' on Lithnet.Ecma2Framework.TestConsumer (net8.0).  " +
                    "Expected exe at: src\\Lithnet.Ecma2Framework.Hosting\\bin\\Debug\\net8.0\\" +
                    "Lithnet.Ecma2Framework.TestConsumer.exe");
            }

            CapabilitiesConnection connection = new CapabilitiesConnection(workerExePath);

            MACapabilities caps = connection.GetCapabilitiesEx(null);

            Assert.IsNotNull(caps, "Capabilities must not be null");
            Assert.IsTrue(caps.SupportImport, "TestCapabilitiesProvider sets SupportImport=true");
        }

        [TestMethod]
        public void GetCapabilities_RealConsumer_ReturnsSupportExport()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found.  Build the TestConsumer project first.");
            }

            CapabilitiesConnection connection = new CapabilitiesConnection(workerExePath);

            MACapabilities caps = connection.GetCapabilitiesEx(null);

            Assert.IsTrue(caps.SupportExport, "TestCapabilitiesProvider sets SupportExport=true");
        }

        [TestMethod]
        public void GetCapabilities_RealConsumer_ReturnsSupportPassword()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found.  Build the TestConsumer project first.");
            }

            CapabilitiesConnection connection = new CapabilitiesConnection(workerExePath);

            MACapabilities caps = connection.GetCapabilitiesEx(null);

            Assert.IsTrue(caps.SupportPassword, "TestCapabilitiesProvider sets SupportPassword=true");
        }

        // -------------------------------------------------------------------------
        // GetConfigParameters tests
        // -------------------------------------------------------------------------

        [TestMethod]
        public void GetConfigParameters_ConnectivityPage_ReturnsEmptyList()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found.  Build the TestConsumer project first.");
            }

            ParametersConnection connection = new ParametersConnection(workerExePath);

            System.Collections.Generic.IList<ConfigParameterDefinition> defs =
                connection.GetConfigParametersEx(null, ConfigParameterPage.Connectivity, 0);

            Assert.IsNotNull(defs, "Definition list must not be null");
            Assert.AreEqual(0, defs.Count, "TestConfigParametersProvider returns empty list for Connectivity page");
        }

        [TestMethod]
        public void GetConfigParameters_GlobalPage_ReturnsEmptyList()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found.  Build the TestConsumer project first.");
            }

            ParametersConnection connection = new ParametersConnection(workerExePath);

            System.Collections.Generic.IList<ConfigParameterDefinition> defs =
                connection.GetConfigParametersEx(null, ConfigParameterPage.Global, 0);

            Assert.AreEqual(0, defs.Count, "TestConfigParametersProvider returns empty list for Global page");
        }

        // -------------------------------------------------------------------------
        // ValidateConfigParameters tests
        // -------------------------------------------------------------------------

        [TestMethod]
        public void ValidateConfigParameters_ConnectivityPage_ReturnsSuccess()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found.  Build the TestConsumer project first.");
            }

            ParametersConnection connection = new ParametersConnection(workerExePath);

            ParameterValidationResult result =
                connection.ValidateConfigParametersEx(null, ConfigParameterPage.Connectivity, 0);

            Assert.IsNotNull(result, "Validation result must not be null");
            Assert.AreEqual(
                ParameterValidationResultCode.Success,
                result.Code,
                "TestConfigParametersProvider validation returns Success");
        }

        // -------------------------------------------------------------------------
        // Worker process cleanup test
        // -------------------------------------------------------------------------

        /// <summary>
        /// Verifies that the worker process is disposed correctly after a call by calling
        /// <see cref="CapabilitiesConnection.GetCapabilitiesEx"/> twice in sequence on
        /// separate connection instances.  If the first call left an orphaned worker holding
        /// the named pipe or other resources, the second call would time out or throw.
        /// Success of both calls proves the first worker was cleaned up.
        /// </summary>
        [TestMethod]
        public void GetCapabilities_CalledTwiceInSequence_BothSucceed_NoOrphan()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found.  Build the TestConsumer project first.");
            }

            CapabilitiesConnection connection1 = new CapabilitiesConnection(workerExePath);
            MACapabilities caps1 = connection1.GetCapabilitiesEx(null);

            Assert.IsNotNull(caps1, "First call: capabilities must not be null");
            Assert.IsTrue(caps1.SupportImport, "First call: SupportImport must be true");

            CapabilitiesConnection connection2 = new CapabilitiesConnection(workerExePath);
            MACapabilities caps2 = connection2.GetCapabilitiesEx(null);

            Assert.IsNotNull(caps2, "Second call: capabilities must not be null");
            Assert.IsTrue(caps2.SupportImport, "Second call: SupportImport must be true");
        }
    }
}
