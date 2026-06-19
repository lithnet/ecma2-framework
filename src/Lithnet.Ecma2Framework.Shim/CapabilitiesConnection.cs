using System;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Implements the host <see cref="IMAExtensible2GetCapabilitiesEx"/> contract by delegating
    /// capability retrieval to the out-of-process worker via the named-pipe JSON-RPC transport.
    /// </summary>
    /// <remarks>
    /// The worker executable path is resolved from the <c>LITHNET_ECMA2_WORKER_EXE</c>
    /// environment variable or from the constructor-injected path (test scenario).
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="GetCapabilitiesEx"/> throws <see cref="InvalidOperationException"/> when
    ///     the worker executable path cannot be resolved, when the worker fails to start,
    ///     or when the JSON-RPC call returns an error.</item>
    ///   <item>The worker process is unconditionally disposed after the RPC call completes
    ///     or fails, so no orphaned process is left behind.</item>
    /// </list>
    ///
    /// TODO(codegen/perf): answer locally from static metadata to avoid worker spawn for config UI.
    /// </remarks>
    internal sealed class CapabilitiesConnection
    {
        private readonly WorkerSession session;

        // -------------------------------------------------------------------------
        // Constructors
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises a <see cref="CapabilitiesConnection"/> that resolves the worker executable
        /// path from the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable.
        /// </summary>
        public CapabilitiesConnection()
        {
            this.session = new WorkerSession();
        }

        /// <summary>
        /// Initialises a <see cref="CapabilitiesConnection"/> with an explicit worker executable path.
        /// Intended for integration tests.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal CapabilitiesConnection(string workerExePath)
        {
            this.session = new WorkerSession(workerExePath);
        }

        // -------------------------------------------------------------------------
        // IMAExtensible2GetCapabilitiesEx
        // -------------------------------------------------------------------------

        /// <summary>
        /// Spawns the worker process, sends a <c>GetCapabilities</c> JSON-RPC call with the
        /// current config parameters, converts the wire response to a real
        /// <see cref="MACapabilities"/>, and disposes the worker.
        /// </summary>
        /// <param name="configParameters">The MA configuration parameters supplied by the host engine.</param>
        /// <returns>A real <see cref="MACapabilities"/> built from the wire response.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved, when the worker fails to start,
        /// or when the JSON-RPC call returns an error.
        /// </exception>
        public MACapabilities GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                JsonRpcPipeClient client = this.session.Open();

                string configParametersXml = ConfigParameterPayload.Serialize(configParameters);

                string capabilitiesXml = client.GetCapabilities(configParametersXml);

                if (string.IsNullOrEmpty(capabilitiesXml))
                {
                    throw new InvalidOperationException(
                        "Worker returned a GetCapabilities response with a null capabilities object.");
                }

                return MmsPipeSerializer.DeserializeXml<MACapabilities>(capabilitiesXml);
            }
            finally
            {
                this.session.Dispose();
            }
        }
    }
}
