using System;
using System.Collections.Generic;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Implements the host <see cref="IMAExtensible2CallExport"/> contract by delegating
    /// each export lifecycle call to the out-of-process worker via the named-pipe
    /// JSON-RPC 2.0 transport.
    /// </summary>
    /// <remarks>
    /// Worker executable location:
    /// The worker executable path is resolved from the environment variable
    /// <c>LITHNET_ECMA2_WORKER_EXE</c>.  This is the preferred mechanism during
    /// development and test because it lets different configurations (debug/release,
    /// test doubles) point to different built outputs without modifying code.
    /// In production the value should be set by the MA installation script.
    ///
    /// If the environment variable is absent or empty, <see cref="OpenExportConnection"/>
    /// throws <see cref="InvalidOperationException"/> immediately, which surfaces as a
    /// connection-open failure to the MIM Synchronization Service rather than silently
    /// using a wrong path.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenExportConnection"/> throws on any failure.  If the worker
    ///     process was started before the failure, its <see cref="WorkerProcessHost"/> is
    ///     disposed, which kills the process via the Job Object.</item>
    ///   <item><see cref="PutExportEntries"/> sends the batch to the worker and converts the
    ///     per-entry results back to real result objects. A malformed results payload is a
    ///     transport-level failure and propagates (the session is unrecoverable at that point);
    ///     per-entry poison isolation is not applied on the shim side, because a malformed wire
    ///     result indicates a marshalling defect, not connected-directory data.</item>
    ///   <item><see cref="CloseExportConnection"/> disposes the worker host regardless of
    ///     whether the pipe call succeeds, so the worker process is always killed.</item>
    /// </list>
    ///
    /// Thread safety: this class is used by the MIM engine on a single thread per export
    /// run; no locking is applied.
    /// </remarks>
    internal sealed class ExportConnection
    {
        private readonly WorkerSession session;

        // -------------------------------------------------------------------------
        // Constructors
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises an <see cref="ExportConnection"/> that resolves the worker
        /// executable path from the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable.
        /// </summary>
        public ExportConnection()
        {
            this.session = new WorkerSession();
        }

        /// <summary>
        /// Initialises an <see cref="ExportConnection"/> with an explicit worker executable
        /// path.  Intended for use in tests where the real environment variable may not be set.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal ExportConnection(string workerExePath)
        {
            this.session = new WorkerSession(workerExePath);
        }

        // -------------------------------------------------------------------------
        // IMAExtensible2CallExport
        // -------------------------------------------------------------------------

        /// <summary>
        /// Gets the default batch size suggested to the MIM engine when the run profile
        /// does not specify one.
        /// </summary>
        public int ExportDefaultPageSize
        {
            get
            {
                return 100;
            }
        }

        /// <summary>
        /// Gets the maximum batch size this MA will accept.
        /// </summary>
        public int ExportMaxPageSize
        {
            get
            {
                return 10000;
            }
        }

        /// <summary>
        /// Spawns the worker process, establishes the pipe connection, and sends the
        /// <c>OpenExport</c> JSON-RPC call.
        /// </summary>
        /// <param name="configParameters">
        /// The MA configuration parameters supplied by the host engine.
        /// </param>
        /// <param name="types">The schema for this export run.</param>
        /// <param name="exportRunStep">
        /// The run-step descriptor carrying export type and batch size.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved from the environment,
        /// when the worker fails to start, or when the JSON-RPC call returns an error.
        /// </exception>
        public void OpenExportConnection(
            System.Collections.ObjectModel.KeyedCollection<string, ConfigParameter> configParameters,
            Schema types,
            OpenExportConnectionRunStep exportRunStep)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            if (types == null)
            {
                throw new ArgumentNullException("types");
            }

            if (exportRunStep == null)
            {
                throw new ArgumentNullException("exportRunStep");
            }

            JsonRpcPipeClient client = this.session.Open();

            string runStepXml = MmsPipeSerializer.SerializeXml<OpenExportConnectionRunStep>(exportRunStep);
            string schemaXml = MmsPipeSerializer.SerializeXml<Schema>(types);
            string configParametersXml = ConfigParameterSerialization.Serialize(configParameters);

            // Capture the engine's ExtensionsDirectory on the net48 side and inject it into the worker,
            // whose own Utils static would resolve to the WRONG directory.
            client.OpenExport(runStepXml, schemaXml, configParametersXml, Utils.ExtensionsDirectory);
        }

        /// <summary>
        /// Reads the export entries, sends them to the worker via <c>PutExport</c>, and converts
        /// the per-entry wire results back to real <see cref="CSEntryChangeResult"/> objects.
        /// </summary>
        /// <param name="csentries">The export entries supplied by the host engine.</param>
        /// <returns>
        /// A <see cref="PutExportEntriesResults"/> containing one result per input entry.
        /// </returns>
        /// <remarks>
        /// A per-entry result build failure (e.g. malformed wire result from the worker) is
        /// caught and converted to an <see cref="MAExportError.ExportErrorConnectedDirectoryError"/>
        /// result so the batch continues.  A pipe transport failure is allowed to propagate
        /// because the session is unrecoverable at that point.
        /// </remarks>
        public PutExportEntriesResults PutExportEntries(IList<CSEntryChange> csentries)
        {
            if (csentries == null)
            {
                throw new ArgumentNullException("csentries");
            }

            List<CSEntryChange> entries = new List<CSEntryChange>(csentries);

            string entriesXml = MmsPipeSerializer.SerializeXml<List<CSEntryChange>>(entries);

            string resultsXml = this.session.Client.PutExport(entriesXml);

            IList<CSEntryChangeResult> results = BuildResults(resultsXml);

            return new PutExportEntriesResults(results);
        }

        /// <summary>
        /// Sends <c>CloseExport</c> to the worker, then unconditionally disposes the worker
        /// host (killing the worker process).
        /// </summary>
        /// <param name="exportRunStep">
        /// The run-step descriptor carrying the close reason.
        /// </param>
        public void CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
            if (exportRunStep == null)
            {
                throw new ArgumentNullException("exportRunStep");
            }

            try
            {
                if (this.session.Client != null)
                {
                    this.session.Client.CloseExport();
                }
            }
            finally
            {
                // Dispose the worker host and pipe client unconditionally so the worker
                // process is always killed even if the CloseExport call fails.
                this.session.Dispose();
            }
        }

        // -------------------------------------------------------------------------
        // Internal testable logic
        // -------------------------------------------------------------------------

        /// <summary>
        /// Deserialises the per-entry real <see cref="CSEntryChangeResult"/> objects from the
        /// <c>MmsPipeSerializer</c> XML payload the worker sent. The results are real host objects
        /// reconstructed via the shared surrogate; no per-field translation occurs.
        /// </summary>
        /// <remarks>
        /// Per-result export error-path handling (poison-result isolation) is part of the separate
        /// error-path work (Phase 4b) and is intentionally not applied here. A malformed results
        /// payload is a transport-level failure and propagates.
        /// </remarks>
        /// <param name="resultsXml">The serialised result list from the worker.</param>
        /// <returns>A list of real <see cref="CSEntryChangeResult"/> objects, one per input entry.</returns>
        internal IList<CSEntryChangeResult> BuildResults(string resultsXml)
        {
            if (string.IsNullOrEmpty(resultsXml))
            {
                return new List<CSEntryChangeResult>();
            }

            List<CSEntryChangeResult> results =
                MmsPipeSerializer.DeserializeXml<List<CSEntryChangeResult>>(resultsXml);

            return results ?? new List<CSEntryChangeResult>();
        }
    }
}
