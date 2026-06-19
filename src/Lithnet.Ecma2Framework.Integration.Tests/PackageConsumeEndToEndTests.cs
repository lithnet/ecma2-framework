using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    /// <summary>
    /// The packaging capstone test (Design C, Task 8). It proves that a clean LIBRARY consumer which adds ONLY
    /// the produced <c>Lithnet.Ecma2Framework</c> holding NuGet package - no in-repo framework ProjectReference -
    /// builds to a worker host exe + the net48 shim, driven entirely by the package's auto-imported props/targets
    /// and its bundled toolchain (Host.proj, Shim.proj, the generator, the shared shim + serialization sources,
    /// and the MMS reference copy), with the Sdk (compile reference) and Hosting (runtime closure) resolved
    /// TRANSITIVELY via the holding package's NuGet dependencies - no version pin, no version stamping.
    ///
    /// The flow, all out-of-process:
    ///   1. dotnet pack ALL THREE packages to artifacts\nupkg (after a full solution build supplies the
    ///      runtime/generator DLLs the holding package assembles into build\): the contracts Sdk
    ///      (Lithnet.Ecma2Framework.Sdk), the host runtime (Lithnet.Ecma2Framework.Hosting), and the holding
    ///      package (Lithnet.Ecma2Framework) the consumer installs.
    ///   2. dotnet build the Lithnet.Ecma2Framework.PackageTests consumer, which restores the holding package
    ///      from the local artifacts\nupkg source (its scoped nuget.config) - NOT from any project reference -
    ///      and through it the Sdk + Hosting transitively.
    ///   3. Assert the consumer output contains Ecma2Host.exe (+ closure) FLAT in the root and
    ///      ecma2\Lithnet.Ecma2Framework.PackageTests.Ecma2.dll (the shim), produced from the packages alone, then RUN
    ///      the host and assert it gets past assembly loading (its transitive closure is complete).
    /// </summary>
    [TestClass]
    public class PackageConsumeEndToEndTests
    {
        private const string SdkProjectRelativePath =
            @"Lithnet.Ecma2Framework.Sdk\Lithnet.Ecma2Framework.Sdk.csproj";

        private const string HostingProjectRelativePath =
            @"Lithnet.Ecma2Framework.Hosting\Lithnet.Ecma2Framework.Hosting.csproj";

        private const string HoldingProjectRelativePath =
            @"Lithnet.Ecma2Framework\Lithnet.Ecma2Framework.csproj";

        private const string PackageTestsProjectRelativePath =
            @"Lithnet.Ecma2Framework.PackageTests\Lithnet.Ecma2Framework.PackageTests.csproj";

        // The local package feed, relative to the REPO ROOT (the parent of src\). This MUST match the feed the
        // consumer's scoped nuget.config reads (its 'ecma2-local' source is '..\..\artifacts\nupkg' relative to
        // src\Lithnet.Ecma2Framework.PackageTests\, i.e. <repo>\artifacts\nupkg). Packing anywhere else would let
        // the consumer restore stale feed content instead of this run's freshly-packed packages.
        private const string NupkgOutputRelativePath = @"artifacts\nupkg";

        private const string HostExeFileName = "Ecma2Host.exe";

        private const string ShimSubfolderName = "ecma2";

        private const string ShimAssemblyFileName = "Lithnet.Ecma2Framework.PackageTests.Ecma2.dll";

        private const string ConsumerTargetFramework = "net8.0";

#if DEBUG
        private const string Configuration = "Debug";
#else
        private const string Configuration = "Release";
#endif

        /// <summary>
        /// Resolves the repository src directory from the test assembly's output location
        /// (...\Tests\bin\&lt;Config&gt;\net48 -&gt; four levels up reaches src\).
        /// </summary>
        private static string ResolveSrcDir()
        {
            string testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.GetFullPath(Path.Combine(testAssemblyDir, "..", "..", "..", ".."));
        }

        [TestMethod]
        public void PackageConsumer_BuildsHostExeAndShim_FromPackageAlone()
        {
            string srcDir = ResolveSrcDir();
            string repoRootDir = Path.GetFullPath(Path.Combine(srcDir, ".."));

            string sdkProject = Path.Combine(srcDir, SdkProjectRelativePath);
            string hostingProject = Path.Combine(srcDir, HostingProjectRelativePath);
            string holdingProject = Path.Combine(srcDir, HoldingProjectRelativePath);
            string packageTestsProject = Path.Combine(srcDir, PackageTestsProjectRelativePath);

            // The local feed is at the repo root (matching the consumer's nuget.config 'ecma2-local' source), NOT
            // under src\. Pack into exactly the directory the consumer restores from.
            string nupkgOutputDir = Path.Combine(repoRootDir, NupkgOutputRelativePath);
            string packageTestsDir = Path.GetDirectoryName(packageTestsProject);

            Assert.IsTrue(File.Exists(sdkProject), string.Format("SDK project not found at '{0}'.", sdkProject));
            Assert.IsTrue(File.Exists(hostingProject), string.Format("Hosting project not found at '{0}'.", hostingProject));
            Assert.IsTrue(File.Exists(holdingProject), string.Format("Holding project not found at '{0}'.", holdingProject));
            Assert.IsTrue(File.Exists(packageTestsProject), string.Format("PackageTests project not found at '{0}'.", packageTestsProject));

            // 1. Pack ALL THREE packages into the local feed. --no-build: the solution build that produced this
            //    test assembly already built the SDK, the Hosting runtime, the holding project, and the sibling
            //    generator DLL. The consumer installs the holding package (Lithnet.Ecma2Framework), which depends
            //    on the Sdk (compile reference) and Hosting (the host's runtime closure - Hosting.dll + the
            //    bundled Serialization.dll + StreamJsonRpc + the Sdk) resolved transitively, so the local feed
            //    MUST contain all three packages for the packaged host build to restore.
            //
            //    VersionSuffix=test makes these pack as the prerelease 3.0.0-test, NOT the framework's stable
            //    VersionPrefix (3.0.0). That stable id+version is the one a real release would publish, so emitting
            //    it from a test run pollutes the shared local feed + global cache and (being a NEWER version than
            //    the dev-loop float's matches) can shadow the 3.0.0-dev.<N> packages the side-by-side consumers
            //    (Okta MA, the Example) build against. The 3.0.0-test suffix never collides with the 3.0.0-dev.*
            //    float and never masquerades as a release.
            RunDotnet(
                srcDir,
                string.Format(
                    "pack \"{0}\" -c {1} -o \"{2}\" --no-build -p:VersionSuffix=test",
                    sdkProject,
                    Configuration,
                    nupkgOutputDir));

            RunDotnet(
                srcDir,
                string.Format(
                    "pack \"{0}\" -c {1} -o \"{2}\" --no-build -p:VersionSuffix=test",
                    hostingProject,
                    Configuration,
                    nupkgOutputDir));

            RunDotnet(
                srcDir,
                string.Format(
                    "pack \"{0}\" -c {1} -o \"{2}\" --no-build -p:VersionSuffix=test",
                    holdingProject,
                    Configuration,
                    nupkgOutputDir));

            // Discover the exact version just packed (the holding package .nupkg file name) so the consumer pins
            // to it regardless of the framework's current <Version>. All three packages are built at the
            // identical version, so this version pins the whole set.
            string packageVersion = ResolvePackedVersion(nupkgOutputDir);

            // 2. Clean the consumer's prior restore + output so the build is a true package-only restore (no stale
            //    package from a previous run, no stale host/shim from a prior build masking a regression).
            ClearDirectory(Path.Combine(packageTestsDir, "obj"));
            ClearDirectory(Path.Combine(packageTestsDir, "bin"));

            // The framework's <Version> is a FIXED string, so every pack produces the identical package id+version.
            // NuGet extracts a given id+version into the GLOBAL packages cache once and never re-extracts an
            // already-present version - even under `--force`, which re-RESOLVES but does not re-EXTRACT. A consumer
            // restore would therefore silently use a STALE extracted copy from a previous pack, testing old package
            // content (the exact trap that let an incomplete host closure pass this test). Evict ALL THREE cached
            // versions - the holding package the consumer installs, plus the Sdk and Hosting packages the build
            // pulls transitively - so this run's freshly-packed content is what gets extracted and consumed.
            EvictPackageFromGlobalCache("Lithnet.Ecma2Framework", packageVersion);
            EvictPackageFromGlobalCache("Lithnet.Ecma2Framework.Sdk", packageVersion);
            EvictPackageFromGlobalCache("Lithnet.Ecma2Framework.Hosting", packageVersion);

            // 3. Build the consumer against the local package source (its scoped nuget.config). --force ensures the
            //    freshly-packed package is re-resolved even if a same-version one was cached previously.
            RunDotnet(
                packageTestsDir,
                string.Format(
                    "build \"{0}\" -c {1} --force -p:Ecma2PackageTestVersion={2}",
                    packageTestsProject,
                    Configuration,
                    packageVersion));

            // 4. Assert the host exe + shim were produced from the package alone.
            string consumerOutputDir = Path.Combine(packageTestsDir, "bin", Configuration, ConsumerTargetFramework);

            string hostExePath = Path.Combine(consumerOutputDir, HostExeFileName);
            string shimPath = Path.Combine(consumerOutputDir, ShimSubfolderName, ShimAssemblyFileName);

            Assert.IsTrue(
                File.Exists(hostExePath),
                string.Format("The packaged consumer build did not produce the host exe at '{0}'.", hostExePath));

            Assert.IsTrue(
                File.Exists(shimPath),
                string.Format("The packaged consumer build did not produce the shim at '{0}'.", shimPath));

            // 5. Asserting the files exist is not enough: a host whose runtime closure is incomplete (e.g. a
            //    missing StreamJsonRpc) still produces the exe, then crashes on its first pipe call with an
            //    assembly-load fault. Launch the produced host and prove it gets PAST assembly loading. With its
            //    closure intact the worker blocks waiting to connect to the (nonexistent) pipe - that block is
            //    success; we kill it after a short grace window.
            AssertHostStartsWithoutAssemblyLoadFault(hostExePath);
        }

        /// <summary>
        /// Launches the produced host exe with <c>--pipe &lt;random&gt;</c>, captures stdout + stderr, and asserts
        /// it does NOT fault with an assembly-load error (the defect class this test exists to catch). With a
        /// complete runtime closure the worker proceeds past assembly loading and blocks trying to connect to the
        /// nonexistent pipe; we give it a short grace window and then kill it. If it exits on its own within the
        /// window, its output must still be free of an unhandled assembly-load fault.
        /// </summary>
        private static void AssertHostStartsWithoutAssemblyLoadFault(string hostExePath)
        {
            const int graceMilliseconds = 5000;

            string pipeName = "ecma2_e2e_probe_" + Guid.NewGuid().ToString("N");

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = hostExePath,
                Arguments = string.Format("--pipe {0}", pipeName),
                WorkingDirectory = Path.GetDirectoryName(hostExePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            StringBuilder output = new StringBuilder();
            bool exitedWithinGrace;

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

                try
                {
                    exitedWithinGrace = process.WaitForExit(graceMilliseconds);
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                        catch (InvalidOperationException)
                        {
                            // The process exited between the HasExited check and Kill; nothing to clean up.
                        }
                    }
                }
            }

            string capturedOutput;
            lock (output)
            {
                capturedOutput = output.ToString();
            }

            string[] assemblyLoadFaultMarkers =
            {
                "Could not load file or assembly",
                "FileNotFoundException",
                "TypeLoadException",
                "FileLoadException",
            };

            foreach (string marker in assemblyLoadFaultMarkers)
            {
                Assert.IsFalse(
                    capturedOutput.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0,
                    string.Format(
                        "The packaged host '{0}' faulted with an assembly-load error (matched '{1}'). The host's "
                        + "runtime closure is incomplete.\n--- host output ---\n{2}",
                        hostExePath,
                        marker,
                        capturedOutput));
            }

            // exitedWithinGrace is informational: a clean usage/exit with no assembly-load fault is acceptable; a
            // worker that blocks on the pipe (does not exit) is the expected healthy path. The marker assertions
            // above are the real gate in both cases.
            _ = exitedWithinGrace;
        }

        /// <summary>
        /// Resolves the version of the most recently produced HOLDING package - <c>Lithnet.Ecma2Framework</c>, the
        /// package the consumer installs - in the output directory by parsing its file name
        /// (<c>Lithnet.Ecma2Framework.&lt;version&gt;.nupkg</c>). The sibling <c>Lithnet.Ecma2Framework.Sdk</c> and
        /// <c>Lithnet.Ecma2Framework.Hosting</c> packages share the id prefix, but their next id segment begins
        /// with a letter (<c>Sdk</c> / <c>Hosting</c>), whereas the holding package's prefix is followed directly
        /// by the version (which begins with a digit). The most recently written matching .nupkg is the one this
        /// run just packed. All three packages are produced at the identical version, so this pins the whole set.
        /// </summary>
        private static string ResolvePackedVersion(string nupkgOutputDir)
        {
            const string packageIdPrefix = "Lithnet.Ecma2Framework.";
            const string nupkgExtension = ".nupkg";

            string newestPackagePath = null;
            string newestVersion = null;
            DateTime newestWriteTime = DateTime.MinValue;

            foreach (string candidate in Directory.GetFiles(nupkgOutputDir, packageIdPrefix + "*" + nupkgExtension))
            {
                string fileName = Path.GetFileName(candidate);

                string afterPrefix = fileName.Substring(
                    packageIdPrefix.Length,
                    fileName.Length - packageIdPrefix.Length - nupkgExtension.Length);

                // The holding package's id is exactly 'Lithnet.Ecma2Framework', so the segment immediately after
                // the prefix is the version (begins with a digit). The Sdk / Hosting siblings share the prefix but
                // their next segment is an id word (begins with a letter): exclude those.
                if (afterPrefix.Length == 0 || !char.IsDigit(afterPrefix[0]))
                {
                    continue;
                }

                DateTime writeTime = File.GetLastWriteTimeUtc(candidate);

                if (writeTime > newestWriteTime)
                {
                    newestWriteTime = writeTime;
                    newestPackagePath = candidate;
                    newestVersion = afterPrefix;
                }
            }

            Assert.IsNotNull(
                newestPackagePath,
                string.Format("No Lithnet.Ecma2Framework holding .nupkg was found in '{0}' after packing.", nupkgOutputDir));

            return newestVersion;
        }

        private static void ClearDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        /// <summary>
        /// Removes a single package id+version from NuGet's global packages cache so a freshly-packed package of
        /// the same version is re-extracted (and thus actually consumed) on the next restore. NuGet does not
        /// re-extract an id+version already present in the global cache, so without this an identical-version
        /// re-pack is masked by the stale extracted copy. The global packages folder honours the
        /// <c>NUGET_PACKAGES</c> environment variable and otherwise defaults to
        /// <c>%USERPROFILE%\.nuget\packages</c>; package folders within it are lower-cased by id.
        /// </summary>
        private static void EvictPackageFromGlobalCache(string packageId, string packageVersion)
        {
            string globalPackagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (string.IsNullOrEmpty(globalPackagesRoot))
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                globalPackagesRoot = Path.Combine(userProfile, ".nuget", "packages");
            }

            string packageVersionDir = Path.Combine(
                globalPackagesRoot,
                packageId.ToLowerInvariant(),
                packageVersion.ToLowerInvariant());

            ClearDirectory(packageVersionDir);
        }

        /// <summary>
        /// Runs the dotnet CLI with the given arguments in the given working directory and fails the test (with
        /// the captured stdout + stderr) if the process exits non-zero.
        /// </summary>
        private static void RunDotnet(string workingDirectory, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
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
                    Assert.Fail(string.Format(
                        "dotnet {0} (in '{1}') failed with exit code {2}.\n--- output ---\n{3}",
                        arguments,
                        workingDirectory,
                        process.ExitCode,
                        output.ToString()));
                }
            }
        }
    }
}
