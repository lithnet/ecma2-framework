using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    // The Shim project reference now exports the shared Lithnet.Ecma2Framework.AttributeType (compiled in as
    // shared transport source). That parent-namespace type would shadow the host AttributeType this file uses;
    // pin the host enum with a namespace-scoped alias (outranks the parent namespace's type). Non-behavioural.
    using AttributeType = Microsoft.MetadirectoryServices.AttributeType;

    /// <summary>
    /// The capstone end-to-end test for the v3 generated model. This test exercises the fully GENERATED wiring against a
    /// real <see cref="IMAExtensible2GetSchema"/> entry point:
    ///
    ///   net48 FIM host (this test)
    ///     -> Assembly.LoadFrom the generated shim (Lithnet.Ecma2Framework.SampleConsumer.Ecma2.dll)
    ///     -> instantiate the shared-source Lithnet.Ecma2Framework.Shim.Ecma2Implementation (compiled into the shim)
    ///     -> cast to IMAExtensible2GetSchema and call GetSchema
    ///        -> internal generated shim connection resolves the worker via WorkerPathResolver, which
    ///           self-identifies the MA from the shim's own assembly name
    ///           (registry key 'Lithnet.Ecma2Framework.SampleConsumer.Ecma2' absent ->
    ///            LITHNET_ECMA2_WORKER_EXE env-var fallback)
    ///        -> spawns the GENERATED worker exe (the SampleConsumer's own compiled output, whose entry
    ///           point is the generated WorkerProgram.Main -> WorkerEntryPoint.RunAsync)
    ///        -> worker builds the DI container from SampleStartup, calls SampleSchemaProvider
    ///        -> schema is marshalled back over the pipe and rebuilt into a real host Schema
    ///
    /// The test asserts the returned Schema EXACTLY matches what SampleSchemaProvider produces, proving the
    /// schema really round-tripped from the provider (not a stub), and that no orphaned worker process
    /// remains once the call completes.
    /// </summary>
    [TestClass]
    public class GeneratedShimEndToEndTests
    {
        private const string ManagementAgentName = "Lithnet.Ecma2Framework.SampleConsumer.Ecma2";

        private const string ShimAssemblyFileName = "Lithnet.Ecma2Framework.SampleConsumer.Ecma2.dll";

        private const string ShimImplementationTypeName = "Lithnet.Ecma2Framework.Shim.Ecma2Implementation";

        // Design C: the worker is the generated host exe (Ecma2Host.exe), built by the framework targets FLAT
        // into the consumer output ROOT (the consumer output + the host form one net8 worker). It is no longer
        // the consumer's own exe. The net48 shim is dropped in the 'ecma2\' subfolder.
        private const string SampleWorkerExeFileName = "Ecma2Host.exe";

        private const string ShimSubfolderName = "ecma2";

        private const string WorkerExeEnvironmentVariable = "LITHNET_ECMA2_WORKER_EXE";

        /// <summary>
        /// Resolves the SampleConsumer output directory (where both the generated worker exe and the
        /// generated shim DLL were dropped) from the test assembly's sibling output directories.
        /// </summary>
        private static string ResolveSampleOutputDir()
        {
            string testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            // Path: ...Tests\bin\<Config>\net48  -- four levels up reaches src\.
            string srcDir = Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));

#if DEBUG
            string config = "Debug";
#else
            string config = "Release";
#endif

            return Path.Combine(
                srcDir,
                "Lithnet.Ecma2Framework.SampleConsumer",
                "bin",
                config,
                "net8.0");
        }

        [TestMethod]
        public void GeneratedShim_GetSchema_RoundTripsToSampleProviderSchema()
        {
            string sampleOutputDir = ResolveSampleOutputDir();
            string shimPath = Path.Combine(sampleOutputDir, ShimSubfolderName, ShimAssemblyFileName);
            string workerExePath = Path.Combine(sampleOutputDir, SampleWorkerExeFileName);

            Assert.IsTrue(
                Directory.Exists(sampleOutputDir),
                string.Format(
                    "SampleConsumer output directory not found at '{0}'. Build the SampleConsumer project first.",
                    sampleOutputDir));

            Assert.IsTrue(
                File.Exists(shimPath),
                string.Format(
                    "Generated shim '{0}' not found in '{1}'. The framework targets must build + copy the shim " +
                    "next to the worker output when the SampleConsumer is built.",
                    ShimAssemblyFileName,
                    sampleOutputDir));

            Assert.IsTrue(
                File.Exists(workerExePath),
                string.Format(
                    "Generated worker exe '{0}' not found in '{1}'.",
                    SampleWorkerExeFileName,
                    sampleOutputDir));

            // Snapshot the worker processes by name BEFORE the call so the no-orphan assertion can detect a
            // new survivor introduced by this test (rather than a pre-existing unrelated process).
            HashSet<int> workerPidsBefore = SnapshotWorkerPids();

            // WorkerPathResolver self-identifies the MA from the shim's own assembly name
            // (Lithnet.Ecma2Framework.SampleConsumer.Ecma2), then looks up the per-MA registry key. That key
            // does not exist in the test environment, so resolution falls through to the env-var: reading an
            // absent HKLM\Software\Lithnet\Ecma2\<MA> key returns null (no admin needed) and the resolver
            // proceeds to LITHNET_ECMA2_WORKER_EXE. This exercises the REAL self-identification + resolution +
            // spawn path: a missing/wrong worker exe would surface as a connection-open failure, not a hang or
            // silent pass.
            string previousEnvValue = Environment.GetEnvironmentVariable(WorkerExeEnvironmentVariable);
            Environment.SetEnvironmentVariable(WorkerExeEnvironmentVariable, workerExePath);

            object schemaObject;

            try
            {
                Assembly shimAssembly = Assembly.LoadFrom(shimPath);

                Type implementationType = shimAssembly.GetType(ShimImplementationTypeName, false);

                Assert.IsNotNull(
                    implementationType,
                    string.Format(
                        "The generated shim must expose the public type '{0}'.",
                        ShimImplementationTypeName));

                object implementation = Activator.CreateInstance(implementationType);

                IMAExtensible2GetSchema schemaProvider = implementation as IMAExtensible2GetSchema;

                Assert.IsNotNull(
                    schemaProvider,
                    string.Format(
                        "The generated type '{0}' must implement IMAExtensible2GetSchema.",
                        ShimImplementationTypeName));

                KeyedCollection<string, ConfigParameter> emptyConfig = new ConfigParameterKeyedCollection();

                Schema schema = schemaProvider.GetSchema(emptyConfig);
                schemaObject = schema;

                AssertSampleSchema(schema);
            }
            finally
            {
                Environment.SetEnvironmentVariable(WorkerExeEnvironmentVariable, previousEnvValue);
            }

            // No-orphan assertion: any worker process spawned during the call must have been terminated by the
            // shim connection (its WorkerProcessHost disposes the Job Object, which kills the worker). Allow a
            // brief window for the OS to record the exit.
            AssertNoOrphanedWorker(workerPidsBefore);

            GC.KeepAlive(schemaObject);
        }

        /// <summary>
        /// Asserts the returned schema EXACTLY matches what <c>SampleSchemaProvider</c> produces: a single
        /// object type "samplePerson" with anchor "id" (String) plus single-valued "displayName" and "email"
        /// (both String, non-anchor).
        /// </summary>
        private static void AssertSampleSchema(Schema schema)
        {
            Assert.IsNotNull(schema, "Schema must not be null");
            Assert.IsNotNull(schema.Types, "Schema.Types must not be null");
            Assert.AreEqual(1, schema.Types.Count, "Schema must contain exactly one object type");

            SchemaType personType = null;

            foreach (SchemaType t in schema.Types)
            {
                if (t.Name == "samplePerson")
                {
                    personType = t;
                    break;
                }
            }

            Assert.IsNotNull(personType, "Schema must contain a type named 'samplePerson'");
            Assert.AreEqual(3, personType.Attributes.Count, "Type 'samplePerson' must have exactly three attributes");

            SchemaAttribute idAttr = null;
            SchemaAttribute displayNameAttr = null;
            SchemaAttribute emailAttr = null;

            foreach (SchemaAttribute a in personType.Attributes)
            {
                if (a.Name == "id")
                {
                    idAttr = a;
                }
                else if (a.Name == "displayName")
                {
                    displayNameAttr = a;
                }
                else if (a.Name == "email")
                {
                    emailAttr = a;
                }
            }

            Assert.IsNotNull(idAttr, "Type 'samplePerson' must contain an attribute named 'id'");
            Assert.IsTrue(idAttr.IsAnchor, "Attribute 'id' must be an anchor");
            Assert.AreEqual(AttributeType.String, idAttr.DataType, "Attribute 'id' must have DataType String");

            Assert.IsNotNull(displayNameAttr, "Type 'samplePerson' must contain an attribute named 'displayName'");
            Assert.IsFalse(displayNameAttr.IsAnchor, "Attribute 'displayName' must not be an anchor");
            Assert.AreEqual(AttributeType.String, displayNameAttr.DataType, "Attribute 'displayName' must have DataType String");

            Assert.IsNotNull(emailAttr, "Type 'samplePerson' must contain an attribute named 'email'");
            Assert.IsFalse(emailAttr.IsAnchor, "Attribute 'email' must not be an anchor");
            Assert.AreEqual(AttributeType.String, emailAttr.DataType, "Attribute 'email' must have DataType String");
        }

        /// <summary>
        /// Returns the set of PIDs of currently-running processes whose name matches the generated worker exe.
        /// </summary>
        private static HashSet<int> SnapshotWorkerPids()
        {
            HashSet<int> pids = new HashSet<int>();

            // Process.GetProcessesByName takes the name WITHOUT extension.
            string processName = Path.GetFileNameWithoutExtension(SampleWorkerExeFileName);

            foreach (Process p in Process.GetProcessesByName(processName))
            {
                try
                {
                    pids.Add(p.Id);
                }
                finally
                {
                    p.Dispose();
                }
            }

            return pids;
        }

        /// <summary>
        /// Asserts that no worker process beyond those present in <paramref name="pidsBefore"/> survives. The
        /// shim's WorkerProcessHost is disposed inside GetSchema's finally, and disposing its Job Object kills
        /// the worker; this confirms no orphan was left behind. Polls briefly to allow the OS to record exits.
        /// </summary>
        private static void AssertNoOrphanedWorker(HashSet<int> pidsBefore)
        {
            const int timeoutMs = 5000;
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (FindNewWorkerPid(pidsBefore) == -1)
                {
                    return;
                }

                System.Threading.Thread.Sleep(100);
            }

            int orphanPid = FindNewWorkerPid(pidsBefore);

            Assert.AreEqual(
                -1,
                orphanPid,
                string.Format(
                    "An orphaned worker process (PID {0}) survived after the generated shim GetSchema call " +
                    "completed. The shim's Job Object should have terminated the spawned worker.",
                    orphanPid));
        }

        /// <summary>
        /// Returns the PID of a worker process that is running now but was not present in
        /// <paramref name="pidsBefore"/>, or -1 if none.
        /// </summary>
        private static int FindNewWorkerPid(HashSet<int> pidsBefore)
        {
            string processName = Path.GetFileNameWithoutExtension(SampleWorkerExeFileName);

            foreach (Process p in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (!pidsBefore.Contains(p.Id))
                    {
                        return p.Id;
                    }
                }
                finally
                {
                    p.Dispose();
                }
            }

            return -1;
        }
    }
}
