using System;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.Internal;
using Lithnet.Ecma2Framework.Hosting.Manifest;

namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// The public entry point for a consumer-hosted worker. In the v3 codegen model the consumer's own
    /// project compiles to the worker executable: a source generator emits a <c>Main</c> in the consumer
    /// assembly that calls <see cref="RunAsync"/> with the consumer's <see cref="IEcmaStartup"/> and the
    /// generated <see cref="IConfigRegistrationProvider"/>. This method owns the pipe-connect plus
    /// JSON-RPC serve loop, delegating the connect/attach/await logic to <see cref="WorkerRunner"/>.
    /// </summary>
    public static class WorkerEntryPoint
    {
        /// <summary>
        /// Builds the DI container from the supplied startup and registration provider, connects to the
        /// named pipe identified by the <c>--pipe</c> argument, and serves JSON-RPC requests until the
        /// shim closes the connection.
        /// </summary>
        /// <param name="startup">The consumer's IEcmaStartup implementation.</param>
        /// <param name="registrationProvider">The generated config registration provider.</param>
        /// <param name="args">The process command line. Must contain <c>--pipe &lt;name&gt;</c>.</param>
        /// <returns>
        /// 0 on clean completion. A non-zero value is returned (with a diagnostic written to
        /// <see cref="Console.Error"/>) when the required <c>--pipe</c> argument is missing, when the
        /// consumer or DI container fails to build, or when the pipe connection or RPC session fails.
        /// The required <c>--pipe</c> argument has no default: a missing value fails loudly rather than
        /// being silently substituted.
        /// </returns>
        public static async Task<int> RunAsync(IEcmaStartup startup, IConfigRegistrationProvider registrationProvider, string[] args)
        {
            string manifestOutputPath = WorkerRunner.ParseNamedArg(args, "--emit-manifest");

            if (manifestOutputPath != null)
            {
                return await EmitManifestAsync(startup, registrationProvider, args, manifestOutputPath);
            }

            string pipeName = WorkerRunner.ParseNamedArg(args, "--pipe");

            if (pipeName == null)
            {
                Console.Error.WriteLine("ERROR: Required argument --pipe <name> is missing.");
                Console.Error.WriteLine("Usage: worker --pipe <pipe-name>");
                return 1;
            }

            WorkerHost workerHost;

            try
            {
                workerHost = WorkerHost.Create(startup, registrationProvider);
                // The DI container is built on the first parameterised RPC (GetSchema / OpenImport /
                // OpenExport / OpenPassword / config-UI pages) so Startup.SetupServices runs with the
                // real configuration parameters. A fresh worker serves exactly one operation, so that
                // first build is this operation's build.
                Console.Error.WriteLine("[worker] Consumer host created.");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[worker] ERROR building consumer host: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }

            return await WorkerRunner.RunPipeLoopAsync(pipeName, new SchemaRpcTarget(workerHost));
        }

        /// <summary>
        /// Runs the build-time <c>--emit-manifest</c> mode: builds the consumer host with no live
        /// connection, reads the real capabilities and config-parameter definitions, and writes a
        /// FIM Packaged-MA manifest to the supplied output path. This path never enters the pipe
        /// serve loop.
        /// </summary>
        /// <remarks>
        /// The MA-identity arguments (<c>--ma-id</c>, <c>--company</c>, <c>--listname</c>,
        /// <c>--help-text</c>, <c>--shim-name</c>, <c>--assembly-version</c>) are supplied and validated by
        /// the MSBuild targets that own the opt-in. <see cref="ManifestEmitter.EmitAsync"/> still validates
        /// them defensively and fails loud rather than emitting a partial manifest. Any failure here
        /// writes a diagnostic to <see cref="Console.Error"/> and returns a non-zero exit code so the
        /// build fails.
        /// </remarks>
        /// <returns>0 when the manifest is written; non-zero on any failure.</returns>
        private static async Task<int> EmitManifestAsync(IEcmaStartup startup, IConfigRegistrationProvider registrationProvider, string[] args, string manifestOutputPath)
        {
            ManifestEmitOptions options = new ManifestEmitOptions
            {
                OutputPath = manifestOutputPath,
                ManagementAgentId = WorkerRunner.ParseNamedArg(args, "--ma-id"),
                Company = WorkerRunner.ParseNamedArg(args, "--company"),
                ListName = WorkerRunner.ParseNamedArg(args, "--listname"),
                HelpText = WorkerRunner.ParseNamedArg(args, "--help-text"),
                ShimFileName = WorkerRunner.ParseNamedArg(args, "--shim-name"),
                AssemblyVersion = WorkerRunner.ParseNamedArg(args, "--assembly-version"),
            };

            try
            {
                WorkerHost workerHost = WorkerHost.Create(startup, registrationProvider);
                await ManifestEmitter.EmitAsync(workerHost, options);
                Console.Out.WriteLine($"[manifest] wrote {options.OutputPath}");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[manifest] ERROR emitting manifest: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }
    }
}
