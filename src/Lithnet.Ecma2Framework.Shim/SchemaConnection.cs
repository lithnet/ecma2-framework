using System;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Implements the host <see cref="IMAExtensible2GetSchema"/> contract by delegating
    /// schema retrieval to the out-of-process worker via the named-pipe JSON-RPC transport.
    /// </summary>
    /// <remarks>
    /// The worker executable path is resolved from the <c>LITHNET_ECMA2_WORKER_EXE</c>
    /// environment variable or from the constructor-injected path (test scenario).
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="GetSchema"/> throws <see cref="InvalidOperationException"/> when
    ///     the worker executable path cannot be resolved, when the worker fails to start,
    ///     or when the JSON-RPC call returns an error.</item>
    ///   <item>The worker process is unconditionally disposed after the RPC call completes
    ///     or fails, so no orphaned process is left behind.</item>
    /// </list>
    ///
    /// TODO(codegen/perf): answer locally from static metadata to avoid worker spawn for config UI.
    /// </remarks>
    internal sealed class SchemaConnection
    {
        private readonly WorkerSession session;

        // -------------------------------------------------------------------------
        // Constructors
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises a <see cref="SchemaConnection"/> that resolves the worker executable
        /// path from the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable.
        /// </summary>
        public SchemaConnection()
        {
            this.session = new WorkerSession();
        }

        /// <summary>
        /// Initialises a <see cref="SchemaConnection"/> with an explicit worker executable path.
        /// Intended for integration tests.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal SchemaConnection(string workerExePath)
        {
            this.session = new WorkerSession(workerExePath);
        }

        // -------------------------------------------------------------------------
        // IMAExtensible2GetSchema
        // -------------------------------------------------------------------------

        /// <summary>
        /// Spawns the worker process, calls <c>GetSchema</c> over the JSON-RPC pipe, converts
        /// the wire schema to a real <see cref="Schema"/>, and disposes the worker.
        /// </summary>
        /// <param name="configParameters">The MA configuration parameters supplied by the host engine.</param>
        /// <returns>A real <see cref="Schema"/> built from the wire response.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved, when the worker fails to start,
        /// or when the JSON-RPC call returns an error.
        /// </exception>
        public Schema GetSchema(KeyedCollection<string, ConfigParameter> configParameters)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            try
            {
                JsonRpcPipeClient client = this.session.Open();

                // Schema discovery is NOT parameter-independent: a provider may call the target system
                // (which needs the connectivity parameters) to enumerate its schema. Forward the real
                // parameters so the worker builds its container with them.
                string configParametersXml = ConfigParameterSerialization.Serialize(configParameters);

                string schemaXml = client.GetSchema(configParametersXml);

                return MmsPipeSerializer.DeserializeXml<Schema>(schemaXml);
            }
            finally
            {
                this.session.Dispose();
            }
        }
    }
}
