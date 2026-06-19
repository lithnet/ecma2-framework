using System;
using System.IO.Pipes;
using System.Threading.Tasks;
using StreamJsonRpc;

namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// Shared worker runtime: command-line argument parsing and the single pipe-connect plus JSON-RPC
    /// serve loop used by the generated, compile-time-bound entry point (<see cref="WorkerEntryPoint"/>).
    /// Keeping the connect/attach/await logic here in one place keeps the entry point thin.
    /// </summary>
    internal static class WorkerRunner
    {
        private const int PipeConnectTimeoutMs = 10000;

        /// <summary>
        /// Parses a named argument of the form <c>--name value</c> from the command line.
        /// Returns the value, or null if the argument is absent or malformed.
        /// </summary>
        public static string ParseNamedArg(string[] args, string argName)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], argName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        /// <summary>
        /// Connects to the named pipe created by the net48 shim and serves JSON-RPC requests
        /// (Content-Length-framed, UTF-8) against the supplied <see cref="SchemaRpcTarget"/> until the
        /// shim closes the connection.
        /// </summary>
        /// <param name="pipeName">The pipe name to connect to. Must already be validated as non-null by the caller.</param>
        /// <param name="target">The JSON-RPC target serving requests for the duration of the session.</param>
        /// <returns>0 on clean completion, non-zero on any failure.</returns>
        public static async Task<int> RunPipeLoopAsync(string pipeName, SchemaRpcTarget target)
        {
            try
            {
                Console.Error.WriteLine($"[worker] Connecting to pipe: {pipeName}");

                // SECURITY: connect with Identification impersonation level so the host (pipe server) can read
                // this worker's identity to confirm it is the same service account before sending anything that
                // crosses the pipe (passwords cross it in plaintext). Identification lets the host identify, not
                // impersonate, this process — least privilege.
                using (NamedPipeClientStream pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous, System.Security.Principal.TokenImpersonationLevel.Identification))
                {
                    pipe.Connect(PipeConnectTimeoutMs);
                    Console.Error.WriteLine("[worker] Connected. Starting JSON-RPC listener.");

                    using (JsonRpc rpc = JsonRpc.Attach(pipe, target))
                    {
                        await rpc.Completion;
                    }
                }

                Console.Error.WriteLine("[worker] RPC session completed. Exiting.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[worker] ERROR: {ex.GetType().Name}: {ex.Message}");
                return 1;
            }
        }
    }
}
