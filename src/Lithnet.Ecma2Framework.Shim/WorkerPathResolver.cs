using System;
using System.Reflection;
using Microsoft.Win32;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Resolves the absolute path to the out-of-process net8 worker executable for a given
    /// management agent. The path is a REQUIRED input: a missing or empty value is a
    /// misconfiguration that must fail loudly rather than be silently substituted with a guess.
    /// </summary>
    /// <remarks>
    /// Management agent identification:
    /// <list type="bullet">
    ///   <item>The parameterless <see cref="Resolve()"/> overload - the form the generated host-facing
    ///     implementations call - self-identifies: it derives the management agent name from the SHIM's
    ///     OWN assembly simple name. This source is shared and compiled into every per-MA shim, so
    ///     <see cref="Assembly.GetExecutingAssembly"/> is the shim assembly (e.g.
    ///     <c>Lithnet.Okta.ManagementAgent.Ecma2.dll</c>), whose simple name IS the management agent
    ///     name. This removes the need for the source generator to bake the MA name into the templates.</item>
    ///   <item>The <see cref="Resolve(string)"/> overload accepts an explicit management agent name and
    ///     is retained for callers that know the name directly (for example tests).</item>
    /// </list>
    ///
    /// Resolution order (both overloads):
    /// <list type="number">
    ///   <item>The <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable. This is the explicit override:
    ///     when set it wins over the installed registry value, letting a developer (or a test) point at a
    ///     specific built worker without touching the registry.</item>
    ///   <item>The per-MA registry value
    ///     <c>HKEY_LOCAL_MACHINE\Software\Lithnet\Ecma2\&lt;managementAgentName&gt;\WorkerPath</c>,
    ///     read from the 64-bit registry view. This is the production mechanism, written by the MA
    ///     installer.</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>If neither source yields a non-empty path, <c>Resolve</c> throws
    ///     <see cref="InvalidOperationException"/>. It never returns null and never substitutes a
    ///     default or guessed path: a wrong/empty worker path must surface as a connection-open
    ///     failure to the MIM Synchronization Service, not be masked into a healthy-looking run.</item>
    ///   <item>An absent environment-variable override is a normal condition and falls through to the
    ///     per-MA registry value. An absent registry key or value is likewise a normal "not configured
    ///     here" condition. Neither is swallowed into a silent null.</item>
    ///   <item>An unexpected registry-read failure (for example access-denied on the key) is NOT
    ///     swallowed. It is allowed to propagate so the real cause surfaces rather than being masked
    ///     as a generic "not configured" message.</item>
    /// </list>
    /// </remarks>
    internal static class WorkerPathResolver
    {
        /// <summary>
        /// The registry sub-key (under HKEY_LOCAL_MACHINE) whose per-MA child keys carry the
        /// <c>WorkerPath</c> value.
        /// </summary>
        private const string RegistryRootSubKey = "Software\\Lithnet\\Ecma2";

        /// <summary>
        /// The registry value name that holds the absolute path to the worker executable.
        /// </summary>
        private const string WorkerPathValueName = "WorkerPath";

        /// <summary>
        /// The environment variable that specifies the absolute path to the worker executable.
        /// Used as the development and test fallback when the registry does not carry a value.
        /// </summary>
        internal const string WorkerExeEnvironmentVariable = "LITHNET_ECMA2_WORKER_EXE";

        /// <summary>
        /// Resolves the absolute path to the worker executable for the management agent that this shim
        /// assembly represents, self-identifying via the shim's own assembly name. This is the form the
        /// generated host-facing implementations call: the management agent name does not need to be baked
        /// into the generated code because it is already the shim assembly's simple name.
        /// </summary>
        /// <returns>A non-empty absolute path to the worker executable.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the executing assembly has no simple name, or when neither the per-MA registry
        /// value nor the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable supplies a non-empty path.
        /// </exception>
        public static string Resolve()
        {
            string managementAgentName = Assembly.GetExecutingAssembly().GetName().Name;

            if (string.IsNullOrEmpty(managementAgentName))
            {
                // The shim assembly always has a simple name; an empty one means the assembly metadata is
                // corrupt. Fail loudly rather than fall through to a nameless registry lookup that would
                // mask the real problem.
                throw new InvalidOperationException(
                    "The shim assembly's simple name could not be determined, so the management agent " +
                    "identity is unknown. The worker executable path cannot be resolved.");
            }

            return Resolve(managementAgentName);
        }

        /// <summary>
        /// Resolves the absolute path to the worker executable for the named management agent.
        /// </summary>
        /// <param name="managementAgentName">
        /// The management agent name, used as the per-MA registry sub-key. When null or empty the
        /// registry lookup is skipped and resolution proceeds straight to the environment variable.
        /// </param>
        /// <returns>A non-empty absolute path to the worker executable.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when neither the per-MA registry value nor the
        /// <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable supplies a non-empty path. The
        /// message names both the registry key and the environment variable so the misconfiguration
        /// can be corrected.
        /// </exception>
        public static string Resolve(string managementAgentName)
        {
            return Resolve(managementAgentName, RegistryHive.LocalMachine, RegistryView.Registry64);
        }

        /// <summary>
        /// Internal test seam: resolves the worker path against an explicit registry hive/view so the
        /// registry-hit branch can be exercised against a writable hive (HKCU) without administrative
        /// HKLM access. Production always calls the public overload, which pins LocalMachine / Registry64.
        /// </summary>
        internal static string Resolve(string managementAgentName, RegistryHive hive, RegistryView view)
        {
            // The environment variable is an explicit override: when set it wins over the installed
            // registry value (e.g. a developer pointing at a freshly built worker). Fall back to the
            // per-MA registry value (the production mechanism, written by the installer) only when the
            // override is absent.
            string envPath = Environment.GetEnvironmentVariable(WorkerExeEnvironmentVariable);

            if (!string.IsNullOrEmpty(envPath))
            {
                return envPath;
            }

            string registryPath = ReadFromRegistry(managementAgentName, hive, view);

            if (!string.IsNullOrEmpty(registryPath))
            {
                return registryPath;
            }

            string hiveDisplay = hive == RegistryHive.LocalMachine ? "HKEY_LOCAL_MACHINE" : hive.ToString();
            string registryKeyPath = hiveDisplay + "\\" + RegistryRootSubKey + "\\" + (managementAgentName ?? string.Empty);

            throw new InvalidOperationException(
                string.Format(
                    "The worker executable path is not configured for management agent '{0}'. " +
                    "Set the '{3}' environment variable to the absolute path of the " +
                    "Lithnet.Ecma2Framework.Hosting executable, or set the '{1}' value under the registry " +
                    "key '{2}'. Neither source supplied a non-empty path.",
                    managementAgentName,
                    WorkerPathValueName,
                    registryKeyPath,
                    WorkerExeEnvironmentVariable));
        }

        /// <summary>
        /// Reads the per-MA <c>WorkerPath</c> value from the 64-bit HKLM registry view. Returns null
        /// when the management agent name is empty, when the key or value is absent, or when the value
        /// is empty. An unexpected access failure is allowed to propagate.
        /// </summary>
        /// <param name="managementAgentName">The management agent name (per-MA sub-key).</param>
        /// <returns>The configured worker path, or null when not present.</returns>
        private static string ReadFromRegistry(string managementAgentName, RegistryHive hive, RegistryView view)
        {
            if (string.IsNullOrEmpty(managementAgentName))
            {
                return null;
            }

            string subKeyPath = RegistryRootSubKey + "\\" + managementAgentName;

            // The production path reads the 64-bit LocalMachine view so a 32-bit host process still sees the
            // value the installer wrote. RegistryView.Registry64 is honoured on 64-bit Windows and is a no-op
            // on 32-bit. The hive/view are parameterised only so a unit test can target a writable hive.
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
            using (RegistryKey subKey = baseKey.OpenSubKey(subKeyPath))
            {
                if (subKey == null)
                {
                    // Key absent: this MA is not configured in the registry. Fall through to the
                    // environment-variable fallback rather than treating this as an error.
                    return null;
                }

                object value = subKey.GetValue(WorkerPathValueName);

                if (value == null)
                {
                    return null;
                }

                string path = value.ToString();

                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                return path;
            }
        }
    }
}
