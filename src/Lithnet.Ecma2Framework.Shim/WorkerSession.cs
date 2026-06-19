using System;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Owns the lifecycle of a single out-of-process worker: resolving the worker executable path,
    /// spawning the worker process, establishing the named-pipe JSON-RPC connection, and tearing both
    /// down. The per-operation connection classes (import, export, password, schema, capabilities,
    /// parameters) each hold a <see cref="WorkerSession"/> and delegate their worker lifecycle to it, so
    /// the spawn/connect/teardown logic lives in exactly one place.
    /// </summary>
    /// <remarks>
    /// Worker path resolution: <see cref="Open"/> uses the constructor-injected path when one was supplied
    /// (the test seam), otherwise <see cref="WorkerPathResolver.Resolve()"/> (the per-MA registry value
    /// written by the installer, then the LITHNET_ECMA2_WORKER_EXE environment variable). A missing path is
    /// a misconfiguration that fails loudly rather than being guessed.
    ///
    /// Lifecycle: one session spawns at most one worker. <see cref="Open"/> creates the pipe server before
    /// starting the worker so the pipe exists when the worker connects, and disposes anything it started if
    /// any step fails (no orphaned worker on a failed open). <see cref="Dispose"/> tears down the pipe client
    /// and the worker host unconditionally, killing the worker process via its job object.
    /// </remarks>
    internal sealed class WorkerSession : IDisposable
    {
        private const int WorkerConnectTimeoutMs = 30000;

        private readonly string workerExePath;

        private WorkerProcessHost host;
        private JsonRpcPipeClient client;

        /// <summary>
        /// Initialises a session that resolves the worker executable path via
        /// <see cref="WorkerPathResolver.Resolve()"/> (production).
        /// </summary>
        public WorkerSession()
        {
            this.workerExePath = null;
        }

        /// <summary>
        /// Initialises a session with an explicit worker executable path. Intended for tests.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal WorkerSession(string workerExePath)
        {
            if (workerExePath == null)
            {
                throw new ArgumentNullException("workerExePath");
            }

            this.workerExePath = workerExePath;
        }

        /// <summary>
        /// Gets the connected pipe client for the running worker, or null before <see cref="Open"/> has
        /// established the connection (or after <see cref="Dispose"/>).
        /// </summary>
        public JsonRpcPipeClient Client
        {
            get
            {
                return this.client;
            }
        }

        /// <summary>
        /// Resolves the worker path, spawns the worker process, and waits for it to connect to a freshly
        /// created pipe. Returns the connected client. If any step fails, anything started is disposed so no
        /// worker is orphaned, and the exception propagates.
        /// </summary>
        /// <returns>The connected <see cref="JsonRpcPipeClient"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved, or the worker fails to start/connect.
        /// </exception>
        public JsonRpcPipeClient Open()
        {
            string resolvedExePath = this.workerExePath ?? WorkerPathResolver.Resolve();

            string pipeName = "lithnet-ecma2-" + Guid.NewGuid().ToString("N");

            WorkerProcessHost startedHost = null;
            JsonRpcPipeClient startedClient = null;

            try
            {
                // Create the pipe server before spawning the worker so the pipe exists when the worker connects.
                startedClient = new JsonRpcPipeClient(pipeName);

                startedHost = new WorkerProcessHost(resolvedExePath, pipeName);
                startedHost.Start();

                startedClient.WaitForConnection(WorkerConnectTimeoutMs);

                this.host = startedHost;
                this.client = startedClient;

                return startedClient;
            }
            catch
            {
                if (startedClient != null)
                {
                    startedClient.Dispose();
                }

                if (startedHost != null)
                {
                    startedHost.Dispose();
                }

                throw;
            }
        }

        /// <summary>
        /// Disposes the pipe client and the worker host unconditionally, killing the worker process. Safe to
        /// call more than once.
        /// </summary>
        public void Dispose()
        {
            if (this.client != null)
            {
                this.client.Dispose();
                this.client = null;
            }

            if (this.host != null)
            {
                this.host.Dispose();
                this.host = null;
            }
        }
    }
}
