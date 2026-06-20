using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using Lithnet.Ecma2Framework;
using Lithnet.Ecma2Framework.Shim;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    /// <summary>
    /// End-to-end integration tests for the password lifecycle over the live named-pipe JSON-RPC
    /// channel. The shim's <see cref="PasswordConnection"/> spawns the self-contained <c>TestConsumer</c>
    /// worker executable, drives the Open → SetPassword/ChangePassword → Close
    /// sequence, and the tests assert success on the happy path, exact host-exception propagation on
    /// the failure path, and worker-process cleanup in all cases.
    /// </summary>
    /// <remarks>
    /// Worker / consumer path resolution mirrors <see cref="ExportLifecycleTests"/> exactly.
    ///
    /// The public <see cref="PasswordConnection.SetPassword(CSEntry, SecureString, PasswordOptions)"/>
    /// takes a live host <see cref="CSEntry"/>, which is abstract and not constructible in a test, so
    /// these tests drive the internal <c>SetPasswordCore</c> / <c>ChangePasswordCore</c> seams with a
    /// hand-built <see cref="CSEntryIdentity"/>. Internal access is granted by
    /// <c>[assembly: InternalsVisibleTo("Lithnet.Ecma2Framework.Integration.Tests")]</c> in the Shim project.
    ///
    /// Expected behaviour from <c>TestPasswordProviderImpl</c>:
    /// a DN containing "failme" (case-insensitive) throws
    /// <see cref="InvalidOperationException"/> with the message "sample password failure". Because that
    /// is a NON-host worker exception, the worker serialises it as a non-host carrier and the shim's
    /// <see cref="MmsExceptionReconstructor"/> reconstructs it as a host
    /// <see cref="ExtensibleExtensionException"/> whose message is
    /// <c>"Worker exception [System.InvalidOperationException]: sample password failure"</c>. That host
    /// exception is thrown directly by the pipe client's error path (it is not an
    /// <see cref="InvalidOperationException"/>, so the <c>SetPasswordCore</c>/<c>ChangePasswordCore</c>
    /// catch-and-rewrap for envelope-less transport failures does not fire).
    ///
    /// Error handling in the test:
    /// <list type="bullet">
    ///   <item>A <c>finally</c> block calls <see cref="DisposeConnectionSafely"/> on any failure so no
    ///     orphaned worker process is left behind.</item>
    ///   <item>The worker-process exit assertion is bounded at 5 s using the same polling helper as
    ///     <see cref="ExportLifecycleTests"/>.</item>
    /// </list>
    ///
    /// Security: no plaintext password is placed in any assertion message.
    /// </remarks>
    [TestClass]
    public class PasswordLifecycleTests
    {
        private const int ProcessExitTimeoutMs = 5000;

        // -------------------------------------------------------------------------
        // Helpers — path resolution and process lifecycle copied from ExportLifecycleTests.
        // -------------------------------------------------------------------------

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
        /// Builds a <see cref="CSEntryIdentity"/> in the same shape the orphaned password tests used:
        /// DN, RDN, ObjectType, and an ObjectClass entry. The worker deserialises this and hands it to
        /// the provider.
        /// </summary>
        private static CSEntryIdentity BuildEntry(string dn, string objectType = "user")
        {
            CSEntryIdentity entry = new CSEntryIdentity();
            entry.DN = dn;
            entry.RDN = dn;
            entry.ObjectType = objectType;
            entry.ObjectClass.Add(objectType);
            return entry;
        }

        private static SecureString MakeSecure(string s)
        {
            SecureString secure = new SecureString();

            if (s != null)
            {
                foreach (char c in s)
                {
                    secure.AppendChar(c);
                }
            }

            secure.MakeReadOnly();
            return secure;
        }

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
        /// Resolves the PID of the most recently started worker process by name. Called immediately
        /// after <see cref="PasswordConnection.OpenPasswordConnection"/> returns so the process is
        /// guaranteed to be running. Returns -1 if no matching process is found.
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
        /// Calls <see cref="PasswordConnection.ClosePasswordConnection"/> without throwing, so cleanup in
        /// a <c>finally</c> block does not mask the original test failure. Calling it a second time after
        /// a successful close is safe — the worker host is already null and the pipe call is a no-op.
        /// </summary>
        private static void DisposeConnectionSafely(PasswordConnection connection)
        {
            if (connection == null)
            {
                return;
            }

            try
            {
                connection.ClosePasswordConnection();
            }
            catch (Exception)
            {
                // Suppress — the worker host Dispose runs unconditionally inside
                // ClosePasswordConnection's finally block, so the process is killed
                // even when the pipe call fails.
            }
        }

        private static string AssertWorkerExeOrFail()
        {
            string workerExePath = ResolveWorkerExePath();

            if (workerExePath == null)
            {
                Assert.Fail(
                    "TestConsumer worker executable not found.  Build the TestConsumer project first: " +
                    "'dotnet build' on Lithnet.Ecma2Framework.TestConsumer (net8.0).  " +
                    "Expected exe at: src\\Lithnet.Ecma2Framework.TestConsumer\\bin\\Debug\\net8.0\\" +
                    "Lithnet.Ecma2Framework.TestConsumer.exe");
            }

            return workerExePath;
        }

        // -------------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------------

        /// <summary>
        /// Drives the full SetPassword lifecycle over the live pipe: Open → SetPasswordCore → Close.
        /// Asserts the call does not throw and the worker process exits within 5 s of close.
        /// </summary>
        [TestMethod]
        public void Password_SetPassword_FullLifecycle_Succeeds_NoOrphan()
        {
            string workerExePath = AssertWorkerExeOrFail();

            PasswordConnection connection = new PasswordConnection(workerExePath);

            int workerPid = -1;

            try
            {
                ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();

                connection.OpenPasswordConnection(configParameters, null);

                // Capture the worker PID immediately after Open — the process is alive at this point.
                workerPid = ResolveWorkerPid();

                CSEntryIdentity identity = BuildEntry("uid=user1,dc=example,dc=com");

                using (SecureString newPassword = MakeSecure("a-secret"))
                {
                    // Must complete without throwing.
                    connection.SetPasswordCore(identity, newPassword, PasswordOptions.None);
                }

                connection.ClosePasswordConnection();

                if (workerPid > 0)
                {
                    bool exited = WaitForProcessExit(workerPid, ProcessExitTimeoutMs);

                    Assert.IsTrue(
                        exited,
                        string.Format(
                            "Worker process (PID {0}) was still running {1} ms after ClosePasswordConnection. " +
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
        /// Drives the full ChangePassword lifecycle over the live pipe: Open → ChangePasswordCore → Close.
        /// Asserts the call does not throw and the worker process exits within 5 s of close.
        /// </summary>
        [TestMethod]
        public void Password_ChangePassword_FullLifecycle_Succeeds_NoOrphan()
        {
            string workerExePath = AssertWorkerExeOrFail();

            PasswordConnection connection = new PasswordConnection(workerExePath);

            int workerPid = -1;

            try
            {
                ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();

                connection.OpenPasswordConnection(configParameters, null);

                workerPid = ResolveWorkerPid();

                CSEntryIdentity identity = BuildEntry("uid=user2,dc=example,dc=com");

                using (SecureString oldPassword = MakeSecure("old"))
                using (SecureString newPassword = MakeSecure("new"))
                {
                    // Must complete without throwing.
                    connection.ChangePasswordCore(identity, oldPassword, newPassword);
                }

                connection.ClosePasswordConnection();

                if (workerPid > 0)
                {
                    bool exited = WaitForProcessExit(workerPid, ProcessExitTimeoutMs);

                    Assert.IsTrue(
                        exited,
                        string.Format(
                            "Worker process (PID {0}) was still running {1} ms after ClosePasswordConnection. " +
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
        /// Drives SetPassword over the live pipe with a "failme" DN and asserts:
        /// <list type="bullet">
        ///   <item>The call throws a host <see cref="ExtensibleExtensionException"/> (the reconstructed
        ///     carrier for the worker's non-host <see cref="InvalidOperationException"/>) whose message
        ///     preserves the provider's "sample password failure" text.</item>
        ///   <item>The connection still closes cleanly and the worker process is not orphaned (exits
        ///     within 5 s).</item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void Password_SetPassword_FailmeDn_ThrowsExactHostException_NoOrphan()
        {
            string workerExePath = AssertWorkerExeOrFail();

            PasswordConnection connection = new PasswordConnection(workerExePath);

            int workerPid = -1;

            try
            {
                ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();

                connection.OpenPasswordConnection(configParameters, null);

                workerPid = ResolveWorkerPid();

                CSEntryIdentity identity = BuildEntry("uid=failme,dc=example,dc=com");

                ExtensibleExtensionException caught = null;

                try
                {
                    using (SecureString newPassword = MakeSecure("irrelevant"))
                    {
                        connection.SetPasswordCore(identity, newPassword, PasswordOptions.None);
                    }
                }
                catch (ExtensibleExtensionException ex)
                {
                    caught = ex;
                }

                Assert.IsNotNull(
                    caught,
                    "SetPasswordCore with a 'failme' DN must throw ExtensibleExtensionException (the reconstructed " +
                    "host carrier for the worker's non-host InvalidOperationException).");

                Assert.IsTrue(
                    caught.Message.IndexOf("sample password failure", StringComparison.Ordinal) >= 0,
                    string.Format(
                        "The thrown exception message must preserve the provider's failure text. Actual message: '{0}'.",
                        caught.Message));

                // The connection must still close cleanly after a provider failure.
                connection.ClosePasswordConnection();

                if (workerPid > 0)
                {
                    bool exited = WaitForProcessExit(workerPid, ProcessExitTimeoutMs);

                    Assert.IsTrue(
                        exited,
                        string.Format(
                            "Worker process (PID {0}) was still running {1} ms after ClosePasswordConnection " +
                            "following a provider failure. The job object did not terminate it.",
                            workerPid,
                            ProcessExitTimeoutMs));
                }
            }
            finally
            {
                DisposeConnectionSafely(connection);
            }
        }
    }
}
