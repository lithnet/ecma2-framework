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
    /// End-to-end integration tests for the export lifecycle over the live named-pipe
    /// JSON-RPC channel.  The shim's <see cref="ExportConnection"/> spawns a real
    /// self-contained <c>TestConsumer</c> worker executable, drives the full
    /// Open → PutExportEntries → Close sequence, and the tests assert per-entry result
    /// isolation, correct error propagation, and worker-process cleanup.
    /// </summary>
    /// <remarks>
    /// Worker location resolution follows the same approach as
    /// <see cref="ImportLifecycleTests"/>: walk four directory levels up from the test
    /// assembly to reach the <c>src\</c> directory, then navigate to the Worker output.
    /// Prefer the apphost exe; return null if only the dll is present (ExportConnection
    /// requires an exe path, not a dll path).
    ///
    /// The <see cref="ExportConnection"/> internal constructor (path-injected) is used
    /// so the test does not depend on the <c>LITHNET_ECMA2_WORKER_EXE</c> environment
    /// variable being set.  Access is granted via
    /// <c>[assembly: InternalsVisibleTo("Lithnet.Ecma2Framework.Integration.Tests")]</c>
    /// in the Shim project.
    ///
    /// Expected behaviour from <c>TestExportProvider</c>:
    /// Entries with <c>ObjectType == "failme"</c> produce a
    /// <see cref="MAExportError.ExportErrorConnectedDirectoryError"/> result with error
    /// name "test-export-error" and a non-empty detail string.  All other entries produce
    /// <see cref="MAExportError.Success"/>.
    ///
    /// Error handling in the test:
    /// <list type="bullet">
    ///   <item>A <c>finally</c> block calls <see cref="DisposeConnectionSafely"/> on any
    ///     failure so no orphaned worker process is left behind.</item>
    ///   <item>The worker-process exit assertion is bounded at 5 s using the same polling
    ///     helper as <see cref="ImportLifecycleTests"/>.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class ExportLifecycleTests
    {
        private const int ProcessExitTimeoutMs = 5000;

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Resolves the worker executable path in the same way as
        /// <see cref="ImportLifecycleTests"/>.  Returns null when the exe is not present.
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
        /// Builds a minimal <see cref="Schema"/> sufficient for a "user" export run.
        /// The schema declares the anchor attribute "id" and a non-anchor attribute
        /// "displayName" so that the host engine accepts the export entries the test builds.
        /// </summary>
        private static Schema BuildUserSchema()
        {
            Schema schema = Schema.Create();

            SchemaType userType = SchemaType.Create("user", false);

            userType.Attributes.Add(
                SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));

            userType.Attributes.Add(
                SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));

            userType.Attributes.Add(
                SchemaAttribute.CreateSingleValuedAttribute("employeeNumber", AttributeType.Integer));

            userType.Attributes.Add(
                SchemaAttribute.CreateSingleValuedAttribute("photo", AttributeType.Binary));

            schema.Types.Add(userType);

            return schema;
        }

        /// <summary>
        /// Builds a single <see cref="CSEntryChange"/> for the export batch.
        /// </summary>
        /// <param name="dn">The distinguished name of the entry.</param>
        /// <param name="objectType">The object type ("user" or "failme").</param>
        /// <param name="anchorValue">The value of the "id" anchor attribute.</param>
        /// <param name="displayName">The value of the "displayName" attribute.</param>
        /// <param name="employeeNumber">The value of the "employeeNumber" attribute.</param>
        /// <param name="photo">The value of the "photo" binary attribute.</param>
        private static CSEntryChange BuildExportEntry(
            string dn,
            string objectType,
            string anchorValue,
            string displayName,
            long employeeNumber,
            byte[] photo)
        {
            CSEntryChange entry = CSEntryChange.Create();

            entry.ObjectModificationType = ObjectModificationType.Add;
            entry.DN = dn;
            entry.ObjectType = objectType;

            entry.AnchorAttributes.Add(AnchorAttribute.Create("id", anchorValue));

            entry.AttributeChanges.Add(
                AttributeChange.CreateAttributeAdd("displayName", new List<object> { displayName }));

            entry.AttributeChanges.Add(
                AttributeChange.CreateAttributeAdd("employeeNumber", new List<object> { employeeNumber }));

            entry.AttributeChanges.Add(
                AttributeChange.CreateAttributeAdd("photo", new List<object> { photo }));

            return entry;
        }

        /// <summary>
        /// Polls until the process with the given PID has exited, or
        /// <paramref name="timeoutMs"/> elapses.
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

        /// <summary>
        /// Resolves the PID of the most recently started worker process by name.
        /// Called immediately after <see cref="ExportConnection.OpenExportConnection"/> returns
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
        /// Calls <see cref="ExportConnection.CloseExportConnection"/> without throwing, so
        /// cleanup in a <c>finally</c> block does not mask the original test failure.
        /// Calling CloseExportConnection a second time after a successful close is safe —
        /// the worker host is already null and the pipe call is a no-op.
        /// </summary>
        private static void DisposeConnectionSafely(ExportConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                CloseExportConnectionRunStep closeRunStep = new CloseExportConnectionRunStep();
                connection.CloseExportConnection(closeRunStep);
            }
            catch (Exception)
            {
                // Suppress — the worker host Dispose runs unconditionally inside
                // CloseExportConnection's finally block, so the process is killed
                // even when the pipe call fails.
            }
        }

        // -------------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------------

        /// <summary>
        /// Drives the full export lifecycle over the live pipe and asserts:
        /// <list type="bullet">
        ///   <item>One result per input entry, keyed by each entry's own Identifier.</item>
        ///   <item>The "failme" entry's result has
        ///     <see cref="MAExportError.ExportErrorConnectedDirectoryError"/> and populated
        ///     <c>ErrorName</c> / <c>ErrorDetail</c>, while the run did not throw.</item>
        ///   <item>The two "user" entries' results have <see cref="MAExportError.Success"/>.</item>
        ///   <item>The worker process exits within 5 s of <c>CloseExportConnection</c>.</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void Export_FullLifecycle_ReturnsPerEntryResults_IsolatesError_NoOrphan()
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

            ExportConnection connection = new ExportConnection(workerExePath);

            int workerPid = -1;

            try
            {
                // --- Step 1: OpenExportConnection ---

                ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();

                Schema schema = BuildUserSchema();

                OpenExportConnectionRunStep openRunStep = new OpenExportConnectionRunStep();

                connection.OpenExportConnection(configParameters, schema, openRunStep);

                // Capture the worker PID immediately after Open — the process is alive at this point.
                workerPid = ResolveWorkerPid();

                // --- Step 2: Build export entries ---

                byte[] photoBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header

                CSEntryChange entryUser1 = BuildExportEntry(
                    "uid=user1,dc=example,dc=com",
                    "user",
                    "user1-anchor",
                    "Alice Smith",
                    1001L,
                    photoBytes);

                CSEntryChange entryFailme = BuildExportEntry(
                    "uid=failme,dc=example,dc=com",
                    "failme",
                    "failme-anchor",
                    "Fail Me",
                    9999L,
                    photoBytes);

                CSEntryChange entryUser2 = BuildExportEntry(
                    "uid=user2,dc=example,dc=com",
                    "user",
                    "user2-anchor",
                    "Bob Jones",
                    1002L,
                    photoBytes);

                List<CSEntryChange> entries = new List<CSEntryChange>
                {
                    entryUser1,
                    entryFailme,
                    entryUser2,
                };

                // Capture each entry's Identifier before the call — these are the keys
                // to look up in the results collection.
                Guid idUser1 = entryUser1.Identifier;
                Guid idFailme = entryFailme.Identifier;
                Guid idUser2 = entryUser2.Identifier;

                // --- Step 3: PutExportEntries ---

                PutExportEntriesResults putResults = connection.PutExportEntries(entries);

                // --- Step 4: Assertions on results ---

                Assert.IsNotNull(
                    putResults,
                    "PutExportEntries must return a non-null result.");

                Assert.IsNotNull(
                    putResults.CSEntryChangeResults,
                    "PutExportEntriesResults.CSEntryChangeResults must not be null.");

                // 4a. One result per input entry.
                Assert.AreEqual(
                    3,
                    putResults.CSEntryChangeResults.Count,
                    string.Format(
                        "Expected 3 results (one per input entry) but received {0}.",
                        putResults.CSEntryChangeResults.Count));

                // 4b. Each input identifier is present as a key in the results collection.
                Assert.IsTrue(
                    putResults.CSEntryChangeResults.Contains(idUser1),
                    string.Format(
                        "Result for user1 entry (Identifier={0}) not found in results.", idUser1));

                Assert.IsTrue(
                    putResults.CSEntryChangeResults.Contains(idFailme),
                    string.Format(
                        "Result for failme entry (Identifier={0}) not found in results.", idFailme));

                Assert.IsTrue(
                    putResults.CSEntryChangeResults.Contains(idUser2),
                    string.Format(
                        "Result for user2 entry (Identifier={0}) not found in results.", idUser2));

                // 4c. The "failme" entry has ExportErrorConnectedDirectoryError with populated error fields.
                CSEntryChangeResult failmeResult = putResults.CSEntryChangeResults[idFailme];

                Assert.AreEqual(
                    MAExportError.ExportErrorConnectedDirectoryError,
                    failmeResult.ErrorCode,
                    string.Format(
                        "failme result must have ExportErrorConnectedDirectoryError but had {0}.",
                        failmeResult.ErrorCode));

                Assert.IsNotNull(
                    failmeResult.ErrorName,
                    "failme result ErrorName must not be null.");

                Assert.IsTrue(
                    failmeResult.ErrorName.Length > 0,
                    "failme result ErrorName must not be empty.");

                Assert.IsNotNull(
                    failmeResult.ErrorDetail,
                    "failme result ErrorDetail must not be null.");

                Assert.IsTrue(
                    failmeResult.ErrorDetail.Length > 0,
                    "failme result ErrorDetail must not be empty.");

                // 4d. The "user" entries have Success — the error was isolated, not fatal.
                CSEntryChangeResult user1Result = putResults.CSEntryChangeResults[idUser1];

                Assert.AreEqual(
                    MAExportError.Success,
                    user1Result.ErrorCode,
                    string.Format(
                        "user1 result must have Success but had {0}.",
                        user1Result.ErrorCode));

                CSEntryChangeResult user2Result = putResults.CSEntryChangeResults[idUser2];

                Assert.AreEqual(
                    MAExportError.Success,
                    user2Result.ErrorCode,
                    string.Format(
                        "user2 result must have Success but had {0}.",
                        user2Result.ErrorCode));

                // --- Step 5: CloseExportConnection ---

                CloseExportConnectionRunStep closeRunStep = new CloseExportConnectionRunStep();
                connection.CloseExportConnection(closeRunStep);

                // --- Step 6: Worker-process exit assertion ---

                if (workerPid > 0)
                {
                    bool exited = WaitForProcessExit(workerPid, ProcessExitTimeoutMs);

                    Assert.IsTrue(
                        exited,
                        string.Format(
                            "Worker process (PID {0}) was still running {1} ms after CloseExportConnection. " +
                            "The job object did not terminate it.",
                            workerPid,
                            ProcessExitTimeoutMs));
                }
            }
            finally
            {
                // Ensure the connection (and its worker process) is always cleaned up even
                // if an assertion fails mid-run, so a failing test never orphans a worker.
                DisposeConnectionSafely(connection);
            }
        }
    }
}
