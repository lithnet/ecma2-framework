using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Implements the host <see cref="IMAExtensible2GetParametersEx"/> contract by delegating
    /// configuration parameter retrieval and validation to the out-of-process worker via the
    /// named-pipe JSON-RPC transport.
    /// </summary>
    /// <remarks>
    /// The worker executable path is resolved from the <c>LITHNET_ECMA2_WORKER_EXE</c>
    /// environment variable or from the constructor-injected path (test scenario).
    ///
    /// One worker process is spawned per host call to avoid retaining process state across
    /// unrelated host UI interactions.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>Both public methods throw <see cref="InvalidOperationException"/> when the worker
    ///     executable path cannot be resolved, when the worker fails to start, or when the
    ///     JSON-RPC call returns an error.</item>
    ///   <item>The worker process is unconditionally disposed in a finally block so no orphaned
    ///     process is left behind.</item>
    /// </list>
    ///
    /// TODO(codegen/perf): answer locally from static metadata to avoid worker spawn for config UI.
    /// </remarks>
    internal sealed class ParametersConnection
    {
        private readonly WorkerSession session;

        // -------------------------------------------------------------------------
        // Constructors
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises a <see cref="ParametersConnection"/> that resolves the worker executable
        /// path from the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable.
        /// </summary>
        public ParametersConnection()
        {
            this.session = new WorkerSession();
        }

        /// <summary>
        /// Initialises a <see cref="ParametersConnection"/> with an explicit worker executable path.
        /// Intended for integration tests.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal ParametersConnection(string workerExePath)
        {
            this.session = new WorkerSession(workerExePath);
        }

        // -------------------------------------------------------------------------
        // IMAExtensible2GetParametersEx
        // -------------------------------------------------------------------------

        /// <summary>
        /// Spawns the worker process, sends a <c>GetConfigParameters</c> JSON-RPC call for
        /// the specified page, converts the wire response to real
        /// <see cref="ConfigParameterDefinition"/> objects, and disposes the worker.
        /// </summary>
        /// <param name="configParameters">The existing configuration parameters.</param>
        /// <param name="page">The page identifier.</param>
        /// <param name="pageNumber">The page number within a multi-page page type.</param>
        /// <returns>A list of real <see cref="ConfigParameterDefinition"/> objects.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved, when the worker fails to start,
        /// or when the JSON-RPC call returns an error.
        /// </exception>
        public IList<ConfigParameterDefinition> GetConfigParametersEx(
            KeyedCollection<string, ConfigParameter> configParameters,
            ConfigParameterPage page,
            int pageNumber)
        {
            try
            {
                JsonRpcPipeClient client = this.session.Open();

                string configParametersXml = ConfigParameterPayload.Serialize(configParameters);

                string definitionsXml = client.GetConfigParameters(configParametersXml, page.ToString(), pageNumber);

                if (string.IsNullOrEmpty(definitionsXml))
                {
                    return new List<ConfigParameterDefinition>();
                }

                List<ConfigParameterDefinition> definitions =
                    MmsPipeSerializer.DeserializeXml<List<ConfigParameterDefinition>>(definitionsXml);

                return definitions ?? new List<ConfigParameterDefinition>();
            }
            finally
            {
                this.session.Dispose();
            }
        }

        /// <summary>
        /// Spawns the worker process, sends a <c>ValidateConfigParameters</c> JSON-RPC call for
        /// the specified page, converts the wire response to a real
        /// <see cref="ParameterValidationResult"/>, and disposes the worker.
        /// </summary>
        /// <param name="configParameters">The configuration parameters to validate.</param>
        /// <param name="page">The page identifier.</param>
        /// <param name="pageNumber">The page number within a multi-page page type.</param>
        /// <returns>A real <see cref="ParameterValidationResult"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved, when the worker fails to start,
        /// or when the JSON-RPC call returns an error.
        /// </exception>
        public ParameterValidationResult ValidateConfigParametersEx(
            KeyedCollection<string, ConfigParameter> configParameters,
            ConfigParameterPage page,
            int pageNumber)
        {
            try
            {
                JsonRpcPipeClient client = this.session.Open();

                string configParametersXml = ConfigParameterPayload.Serialize(configParameters);

                string resultXml = client.ValidateConfigParameters(configParametersXml, page.ToString(), pageNumber);

                if (string.IsNullOrEmpty(resultXml))
                {
                    // Worker returned null result — treat as success to avoid breaking the host UI.
                    return new ParameterValidationResult(ParameterValidationResultCode.Success, null, null);
                }

                return MmsPipeSerializer.DeserializeXml<ParameterValidationResult>(resultXml);
            }
            finally
            {
                this.session.Dispose();
            }
        }
    }
}
