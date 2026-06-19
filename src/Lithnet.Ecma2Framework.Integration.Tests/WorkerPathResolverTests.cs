using System;
using Lithnet.Ecma2Framework.Shim;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    /// <summary>
    /// Unit tests for <see cref="WorkerPathResolver"/>'s three resolution branches: the per-MA registry
    /// value, the environment-variable fallback, and the fail-loud when neither is configured.
    /// </summary>
    /// <remarks>
    /// The registry-hit branch is the one the production path (HKLM\Registry64, written by the installer)
    /// makes hard to cover without administrative rights. These tests exercise it through the internal
    /// hive/view seam against <c>HKEY_CURRENT_USER</c>, which is writable without elevation, so the actual
    /// registry-reading code runs against a real key. Each test writes and removes its own uniquely-named
    /// key and saves/restores the process environment variable in a <c>finally</c> block.
    /// </remarks>
    [TestClass]
    public class WorkerPathResolverTests
    {
        private const string TestSubKeyRoot = "Software\\Lithnet\\Ecma2";
        private const string WorkerPathValueName = "WorkerPath";
        private const string EnvVar = "LITHNET_ECMA2_WORKER_EXE";

        private static string UniqueMaName()
        {
            return "UnitTestMA_" + Guid.NewGuid().ToString("N");
        }

        private static void WriteRegistryWorkerPath(string maName, string value)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(TestSubKeyRoot + "\\" + maName))
            {
                key.SetValue(WorkerPathValueName, value, RegistryValueKind.String);
            }
        }

        private static void DeleteRegistryKey(string maName)
        {
            Registry.CurrentUser.DeleteSubKeyTree(TestSubKeyRoot + "\\" + maName, false);
        }

        [TestMethod]
        public void EnvVar_WinsOverRegistryValue()
        {
            string maName = UniqueMaName();
            string registryPath = @"C:\registry\worker.exe";
            string envPath = @"C:\env\worker.exe";
            string previousEnv = Environment.GetEnvironmentVariable(EnvVar);

            try
            {
                WriteRegistryWorkerPath(maName, registryPath);

                // Set the env var to a DIFFERENT value to prove the override short-circuits the registry.
                Environment.SetEnvironmentVariable(EnvVar, envPath);

                string resolved = WorkerPathResolver.Resolve(maName, RegistryHive.CurrentUser, RegistryView.Default);

                Assert.AreEqual(
                    envPath,
                    resolved,
                    "The environment variable is the override and must win over the registry WorkerPath value.");
            }
            finally
            {
                DeleteRegistryKey(maName);
                Environment.SetEnvironmentVariable(EnvVar, previousEnv);
            }
        }

        [TestMethod]
        public void RegistryValue_IsUsed_WhenNoEnvVar()
        {
            string maName = UniqueMaName();
            string registryPath = @"C:\registry\worker.exe";
            string previousEnv = Environment.GetEnvironmentVariable(EnvVar);

            try
            {
                WriteRegistryWorkerPath(maName, registryPath);
                Environment.SetEnvironmentVariable(EnvVar, null);

                string resolved = WorkerPathResolver.Resolve(maName, RegistryHive.CurrentUser, RegistryView.Default);

                Assert.AreEqual(
                    registryPath,
                    resolved,
                    "With no environment-variable override, the per-MA registry value must be used.");
            }
            finally
            {
                DeleteRegistryKey(maName);
                Environment.SetEnvironmentVariable(EnvVar, previousEnv);
            }
        }

        [TestMethod]
        public void EnvVar_IsUsed_WhenNoRegistryValue()
        {
            string maName = UniqueMaName(); // never written to the registry
            string envPath = @"C:\env\worker.exe";
            string previousEnv = Environment.GetEnvironmentVariable(EnvVar);

            try
            {
                Environment.SetEnvironmentVariable(EnvVar, envPath);

                string resolved = WorkerPathResolver.Resolve(maName, RegistryHive.CurrentUser, RegistryView.Default);

                Assert.AreEqual(
                    envPath,
                    resolved,
                    "With no registry value present, the environment variable must be used as the fallback.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, previousEnv);
            }
        }

        [TestMethod]
        public void Throws_WhenNeitherRegistryNorEnvVarConfigured()
        {
            string maName = UniqueMaName();
            string previousEnv = Environment.GetEnvironmentVariable(EnvVar);

            try
            {
                Environment.SetEnvironmentVariable(EnvVar, null);

                Assert.Throws<InvalidOperationException>(
                    () => WorkerPathResolver.Resolve(maName, RegistryHive.CurrentUser, RegistryView.Default),
                    "With neither a registry value nor the environment variable, resolution must fail loudly rather than guess.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, previousEnv);
            }
        }
    }
}
