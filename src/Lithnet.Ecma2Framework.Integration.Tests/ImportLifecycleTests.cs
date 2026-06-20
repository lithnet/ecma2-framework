using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Lithnet.Ecma2Framework.Shim;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    // The Shim project reference now exports the shared Lithnet.Ecma2Framework.AttributeType (compiled in as
    // shared transport source). That parent-namespace type would shadow the host AttributeType this file uses;
    // pin the host enum with a namespace-scoped alias (outranks the parent namespace's type). Non-behavioural.
    using AttributeType = Microsoft.MetadirectoryServices.AttributeType;

    /// <summary>
    /// End-to-end integration tests for the import lifecycle over the live named-pipe
    /// JSON-RPC channel driven by a real consumer (<c>TestConsumer</c>).
    /// The shim's <see cref="ImportConnection"/> spawns the self-contained <c>TestConsumer</c> worker
    /// executable, drives the full Open → GetImportEntries* → Close
    /// sequence, and the tests assert correct paging, entry fidelity, and worker-process cleanup.
    /// </summary>
    /// <remarks>
    /// Expected data from <c>TestImportProvider</c>:
    /// 3 entries total — all are well-formed Add entries for object type "user" with anchor "id"
    /// (String) and a "displayName" String attribute.  The TestImportProvider also returns a fixed
    /// outbound watermark "test-watermark-v1" for the "user" type, which the orchestrator serialises
    /// to JSON and returns in the CloseImport response.
    ///
    /// Worker location resolution:
    /// Walk four directory levels up from the test assembly to reach the <c>src\</c> directory,
    /// then navigate to both the Worker output and the TestConsumer output.
    /// Prefer the apphost exe; skip the test with a clear failure if neither exe nor dll is present.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A <c>finally</c> block disposes the connection on failure so no orphaned worker
    ///     process is left behind.</item>
    ///   <item>The GetImportEntries loop is bounded at 20 iterations to prevent an infinite loop
    ///     if the worker misbehaves.</item>
    ///   <item>The worker-process exit assertion is bounded at 5 s and uses a polling helper.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class ImportLifecycleTests
    {
        private const int ProcessExitTimeoutMs = 5000;
        private const int MaxGetImportEntriesIterations = 20;

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Resolves the worker executable path, preferring the apphost exe.
        /// Returns null when neither the exe nor the dll is present.
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
        /// Builds a <see cref="Schema"/> matching the TestConsumer's TestSchemaProvider output:
        /// one object type "user" with anchor "id" (String) and "displayName" (String).
        /// </summary>
        private static Schema BuildUserSchema()
        {
            Schema schema = Schema.Create();

            SchemaType userType = SchemaType.Create("user", false);

            userType.Attributes.Add(
                SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));

            userType.Attributes.Add(
                SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));

            schema.Types.Add(userType);

            return schema;
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
        // Tests
        // -------------------------------------------------------------------------

        /// <summary>
        /// Drives the full import lifecycle over the live pipe with a real consumer and asserts:
        /// <list type="bullet">
        ///   <item>All 3 entries from <c>TestImportProvider</c> are delivered across paged calls.</item>
        ///   <item>All entries have <c>Success</c> error codes and object type "user".</item>
        ///   <item>Each entry has anchor "id" and a "displayName" attribute change.</item>
        ///   <item>Paging is exercised: with the default page size of 1, multiple GetImportEntries
        ///     calls are required to retrieve all 3 entries.</item>
        ///   <item>The worker process exits within 5 s of <c>CloseImportConnection</c>.</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void Import_FullLifecycle_RealConsumer_PagesEntries_NoOrphan()
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

            ImportConnection connection = new ImportConnection(workerExePath);

            int workerPid = -1;
            List<CSEntryChange> allEntries = new List<CSEntryChange>();

            try
            {
                // --- Step 1: OpenImportConnection ---

                ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();
                Schema schema = BuildUserSchema();
                OpenImportConnectionRunStep openRunStep = new OpenImportConnectionRunStep();

                OpenImportConnectionResults openResults = connection.OpenImportConnection(
                    configParameters,
                    schema,
                    openRunStep);

                Assert.IsNotNull(openResults, "OpenImportConnection must return a non-null result");

                // Capture the worker PID via the process name immediately after Open.
                workerPid = ResolveWorkerPid();

                // --- Step 2: GetImportEntries loop ---

                int callCount = 0;
                bool moreToImport = true;

                while (moreToImport)
                {
                    callCount++;

                    Assert.IsTrue(
                        callCount <= MaxGetImportEntriesIterations,
                        string.Format(
                            "GetImportEntries was called {0} times without MoreToImport becoming false. " +
                            "The worker may be stuck in an infinite page loop.",
                            callCount));

                    GetImportEntriesRunStep getRunStep = new GetImportEntriesRunStep();
                    GetImportEntriesResults pageResults = connection.GetImportEntries(getRunStep);

                    Assert.IsNotNull(pageResults, "GetImportEntries must return a non-null result");
                    Assert.IsNotNull(pageResults.CSEntries, "GetImportEntriesResults.CSEntries must not be null");

                    allEntries.AddRange(pageResults.CSEntries);

                    moreToImport = pageResults.MoreToImport;
                }

                // --- Step 3: Assertions on accumulated entries ---

                // 3a. Total entry count: TestImportProvider yields exactly 3 entries.
                Assert.AreEqual(
                    3,
                    allEntries.Count,
                    string.Format(
                        "Expected 3 total entries from TestImportProvider but received {0}.",
                        allEntries.Count));

                // 3b. Paging actually happened: with page size 1 we need at least 2 calls.
                Assert.IsTrue(
                    callCount >= 2,
                    string.Format(
                        "Expected at least 2 GetImportEntries calls (paging) but only made {0}.",
                        callCount));

                // 3c. All entries should be successful — TestImportProvider has no poison entries.
                foreach (CSEntryChange entry in allEntries)
                {
                    Assert.AreEqual(
                        MAImportError.Success,
                        entry.ErrorCodeImport,
                        string.Format(
                            "Entry {0} must have Success error code, but got {1}.",
                            entry.DN,
                            entry.ErrorCodeImport));
                }

                // 3d. All entries have the correct object type.
                foreach (CSEntryChange entry in allEntries)
                {
                    Assert.AreEqual(
                        "user",
                        entry.ObjectType,
                        string.Format(
                            "Entry must have object type 'user' but got '{0}'.",
                            entry.ObjectType));
                }

                // 3e. All entries have the anchor attribute "id".
                foreach (CSEntryChange entry in allEntries)
                {
                    Assert.IsTrue(
                        entry.AnchorAttributes.Contains("id"),
                        "Entry must have anchor attribute 'id'.");
                }

                // 3f. All entries have a "displayName" attribute change.
                foreach (CSEntryChange entry in allEntries)
                {
                    Assert.IsTrue(
                        entry.AttributeChanges.Contains("displayName"),
                        "Entry must have a 'displayName' attribute change.");
                }

                // 3g. Spot-check: displayName values are non-empty strings.
                foreach (CSEntryChange entry in allEntries)
                {
                    object displayNameValue = entry.AttributeChanges["displayName"].ValueChanges[0].Value;

                    Assert.IsInstanceOfType(
                        displayNameValue,
                        typeof(string),
                        string.Format(
                            "displayName value must be a string but was {0}.",
                            displayNameValue == null ? "null" : displayNameValue.GetType().FullName));

                    Assert.IsFalse(
                        string.IsNullOrEmpty((string)displayNameValue),
                        "displayName value must be non-empty.");
                }

                // --- Step 4: CloseImportConnection ---
                CloseImportConnectionRunStep closeRunStep = new CloseImportConnectionRunStep();
                CloseImportConnectionResults closeResults = connection.CloseImportConnection(closeRunStep);

                Assert.IsNotNull(closeResults, "CloseImportConnection must return a non-null result");

                // --- Step 5: Worker-process exit assertion ---
                if (workerPid > 0)
                {
                    bool exited = WaitForProcessExit(workerPid, ProcessExitTimeoutMs);

                    Assert.IsTrue(
                        exited,
                        string.Format(
                            "Worker process (PID {0}) was still running {1} ms after CloseImportConnection. " +
                            "The job object did not terminate it.",
                            workerPid,
                            ProcessExitTimeoutMs));
                }
            }
            finally
            {
                DisposeConnectionSafely(connection);
            }
        }

        /// <summary>
        /// Regression test for the config-parameter pipeline: a configuration parameter supplied to
        /// <c>OpenImportConnection</c> must reach the import provider in the worker. <c>TestImportProvider</c>
        /// echoes the <c>importEchoValue</c> parameter back as an extra "echo" entry; this test sets the
        /// parameter and asserts that entry (carrying the exact value) is imported. Before the fix,
        /// <c>OpenImport</c> carried no config parameters and the worker built its container empty, so this
        /// entry never appeared.
        /// </summary>
        [TestMethod]
        public void Import_ConfigParameter_ReachesProvider_NoOrphan()
        {
            string workerExePath = ResolveWorkerExePath();

            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker executable not found — build the TestConsumer project first.");
            }

            const string echoValue = "reached-the-provider";

            ImportConnection connection = new ImportConnection(workerExePath);
            int workerPid = -1;
            List<CSEntryChange> allEntries = new List<CSEntryChange>();

            try
            {
                ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();
                configParameters.Add(new ConfigParameter("importEchoValue", echoValue));

                Schema schema = BuildUserSchema();
                OpenImportConnectionRunStep openRunStep = new OpenImportConnectionRunStep();

                OpenImportConnectionResults openResults = connection.OpenImportConnection(
                    configParameters, schema, openRunStep);

                Assert.IsNotNull(openResults, "OpenImportConnection must return a non-null result");

                workerPid = ResolveWorkerPid();

                bool moreToImport = true;
                int callCount = 0;

                while (moreToImport)
                {
                    callCount++;

                    Assert.IsTrue(
                        callCount <= MaxGetImportEntriesIterations,
                        "GetImportEntries exceeded the safety iteration limit.");

                    GetImportEntriesResults pageResults = connection.GetImportEntries(new GetImportEntriesRunStep());

                    Assert.IsNotNull(pageResults, "GetImportEntries must return a non-null result");
                    allEntries.AddRange(pageResults.CSEntries);
                    moreToImport = pageResults.MoreToImport;
                }

                // The provider emits its 3 fixed entries plus one "echo" entry carrying the parameter value.
                Assert.AreEqual(
                    4,
                    allEntries.Count,
                    "Expected 4 entries (3 fixed + 1 echo); the config parameter did not reach the provider.");

                CSEntryChange echoEntry = allEntries.Find(
                    e => e.AnchorAttributes.Contains("id") && "echo".Equals(e.AnchorAttributes["id"].Value));

                Assert.IsNotNull(echoEntry, "The echo entry (anchor id 'echo') must be present.");

                object displayName = echoEntry.AttributeChanges["displayName"].ValueChanges[0].Value;

                Assert.AreEqual(
                    echoValue,
                    displayName,
                    "The echo entry's displayName must equal the config-parameter value the provider read.");

                connection.CloseImportConnection(new CloseImportConnectionRunStep());

                if (workerPid > 0)
                {
                    Assert.IsTrue(
                        WaitForProcessExit(workerPid, ProcessExitTimeoutMs),
                        string.Format("Worker process (PID {0}) did not exit after close.", workerPid));
                }
            }
            finally
            {
                DisposeConnectionSafely(connection);
            }
        }

        /// <summary>
        /// Same lifecycle as <see cref="Import_FullLifecycle_RealConsumer_PagesEntries_NoOrphan"/>
        /// run a second time to assert deterministic behaviour across repeated runs.
        /// </summary>
        [TestMethod]
        public void Import_FullLifecycle_RepeatedRun_IsDeterministic()
        {
            string workerExePath = ResolveWorkerExePath();
            if (workerExePath == null)
            {
                Assert.Fail("TestConsumer worker exe not found — build the TestConsumer project first.");
            }

            for (int run = 1; run <= 2; run++)
            {
                ImportConnection connection = new ImportConnection(workerExePath);
                List<CSEntryChange> runEntries = new List<CSEntryChange>();

                try
                {
                    ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();
                    Schema schema = BuildUserSchema();
                    OpenImportConnectionRunStep openRunStep = new OpenImportConnectionRunStep();

                    OpenImportConnectionResults openResults = connection.OpenImportConnection(
                        configParameters, schema, openRunStep);

                    Assert.IsNotNull(openResults, string.Format("Run {0}: OpenImportConnection result must not be null", run));

                    bool moreToImport = true;
                    int iterations = 0;

                    while (moreToImport)
                    {
                        iterations++;

                        Assert.IsTrue(
                            iterations <= MaxGetImportEntriesIterations,
                            string.Format("Run {0}: GetImportEntries loop exceeded safety limit", run));

                        GetImportEntriesResults pageResults = connection.GetImportEntries(
                            new GetImportEntriesRunStep());

                        runEntries.AddRange(pageResults.CSEntries);
                        moreToImport = pageResults.MoreToImport;
                    }

                    Assert.AreEqual(
                        3,
                        runEntries.Count,
                        string.Format("Run {0}: Expected 3 entries but got {1}", run, runEntries.Count));

                    CloseImportConnectionResults closeResults = connection.CloseImportConnection(
                        new CloseImportConnectionRunStep());

                    Assert.IsNotNull(
                        closeResults,
                        string.Format("Run {0}: CloseImportConnection result must not be null", run));
                }
                finally
                {
                    DisposeConnectionSafely(connection);
                }
            }
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Resolves the PID of the most recently started worker process by name.
        /// Called immediately after <see cref="ImportConnection.OpenImportConnection"/> returns
        /// so the process is guaranteed to be running.
        /// Returns -1 if no matching process is found.
        /// </summary>
        private static int ResolveWorkerPid()
        {
            Process[] candidates = Process.GetProcessesByName("Lithnet.Ecma2Framework.TestConsumer");

            if (candidates.Length == 0)
            {
                return -1;
            }

            // If multiple worker processes are running (e.g. parallel test runs), pick the
            // one with the highest StartTime as it is the one we just spawned.
            Process newest = candidates[0];

            for (int i = 1; i < candidates.Length; i++)
            {
                try
                {
                    if (candidates[i].StartTime > newest.StartTime)
                    {
                        newest = candidates[i];
                    }
                }
                catch (InvalidOperationException)
                {
                    // Process may have already exited — ignore.
                }
            }

            try
            {
                return newest.Id;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        /// <summary>
        /// Disposes the <paramref name="connection"/> without throwing, so cleanup in a
        /// <c>finally</c> block does not mask the original test failure.
        /// </summary>
        private static void DisposeConnectionSafely(ImportConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                CloseImportConnectionRunStep closeRunStep = new CloseImportConnectionRunStep();
                connection.CloseImportConnection(closeRunStep);
            }
            catch (Exception)
            {
                // Suppress — the worker host Dispose runs unconditionally inside
                // CloseImportConnection's finally block, so the process is killed
                // even when the pipe call fails.
            }
        }
    }
}
