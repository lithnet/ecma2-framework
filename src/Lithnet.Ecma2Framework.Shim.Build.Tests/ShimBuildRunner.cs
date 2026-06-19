using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Lithnet.Ecma2Framework.Shim.Build.Tests
{
    /// <summary>
    /// Invokes the static shim project (Lithnet.Ecma2Framework.Shim.proj) through the real build mechanism
    /// - a <c>dotnet build</c> of the .proj with a supplied management agent name into an isolated output
    /// root - and returns the path to the produced shim assembly. This exercises the same path the
    /// consumer-facing BuildEcma2Shim target uses, so a template signature error or a closure regression
    /// surfaces as a build failure here.
    /// </summary>
    internal sealed class ShimBuildRunner
    {
        private readonly string repositorySrcDirectory;

        public ShimBuildRunner()
        {
            this.repositorySrcDirectory = LocateRepositorySrcDirectory();
        }

        /// <summary>
        /// The absolute path to the static shim .proj.
        /// </summary>
        public string ShimProjectPath
        {
            get
            {
                return Path.Combine(this.repositorySrcDirectory, "Lithnet.Ecma2Framework", "build", "Lithnet.Ecma2Framework.Shim.proj");
            }
        }

        /// <summary>
        /// Builds the shim for the given management agent name into <paramref name="outputRoot"/> and returns
        /// the absolute path to the produced <c>&lt;managementAgentName&gt;.dll</c>. Throws when the build
        /// fails or the expected output is absent - a missing assembly is a hard failure, never silently
        /// tolerated.
        /// </summary>
        public string BuildShim(string managementAgentName, string configuration, string outputRoot)
        {
            // Pass the output root WITHOUT a trailing separator. A trailing backslash inside a quoted
            // command-line argument escapes the closing quote on Windows, which corrupts the property value;
            // the shim .proj normalizes a missing trailing separator itself (MSBuild EnsureTrailingSlash).
            string trimmedOutputRoot = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // The shim .proj has NO default Ecma2HostMmsPath: the framework derives it from the consumer's own
            // Microsoft.MetadirectoryServicesEx reference and forwards it via -p:Ecma2HostMmsPath. This test
            // invokes the .proj directly (standing in for the BuildEcma2Shim target), so it must supply that path
            // too - pointing at the framework repo's reference copy, which is the in-repo MMS assembly.
            string hostMmsPath = Path.Combine(
                this.repositorySrcDirectory,
                "Lithnet.Ecma2Framework.Sdk",
                "lib",
                "Microsoft.MetadirectoryServicesEx.dll");

            string arguments = string.Format(
                "build \"{0}\" -c {1} -p:Ecma2ManagementAgentName={2} -p:Ecma2HostMmsPath=\"{3}\" -p:Ecma2ShimOutputRoot=\"{4}\"",
                this.ShimProjectPath,
                configuration,
                managementAgentName,
                hostMmsPath,
                trimmedOutputRoot);

            RunDotnet(arguments);

            string expectedDll = Path.Combine(trimmedOutputRoot, "bin", configuration, "net48", managementAgentName + ".dll");

            if (!File.Exists(expectedDll))
            {
                throw new InvalidOperationException("The shim build completed but the expected assembly was not found at: " + expectedDll);
            }

            return expectedDll;
        }

        /// <summary>
        /// Runs <c>dotnet</c> with the supplied arguments, capturing stdout/stderr and throwing with the
        /// captured output when the process exits non-zero.
        /// </summary>
        private static void RunDotnet(string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            StringBuilder output = new StringBuilder();

            using (Process process = new Process())
            {
                process.StartInfo = startInfo;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (output)
                        {
                            output.AppendLine(e.Data);
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (output)
                        {
                            output.AppendLine(e.Data);
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException("The shim build failed (exit code " + process.ExitCode + "). Build output:" + Environment.NewLine + output.ToString());
                }
            }
        }

        /// <summary>
        /// Walks up from the test assembly's location to find the repository <c>src</c> directory - the one
        /// that contains the static shim project. This keeps the test independent of the working directory
        /// the test runner happens to use.
        /// </summary>
        private static string LocateRepositorySrcDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "Lithnet.Ecma2Framework", "build", "Lithnet.Ecma2Framework.Shim.proj");

                if (File.Exists(candidate))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not locate the repository 'src' directory (the one containing Lithnet.Ecma2Framework\\build\\Lithnet.Ecma2Framework.Shim.proj) by walking up from " + AppContext.BaseDirectory + ".");
        }
    }
}
