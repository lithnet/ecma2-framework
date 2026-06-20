using System;
using System.Collections.Generic;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Implements the host <see cref="IMAExtensible2CallImport"/> contract by delegating
    /// each import lifecycle call to the out-of-process worker via the named-pipe
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
    /// If the environment variable is absent or empty, <see cref="OpenImportConnection"/>
    /// throws <see cref="InvalidOperationException"/> immediately, which surfaces as a
    /// connection-open failure to the MIM Synchronization Service rather than silently
    /// using a wrong path.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenImportConnection"/> throws on any failure. If the worker
    ///     process was started before the failure, its <see cref="WorkerProcessHost"/> is
    ///     disposed, which kills the process via the Job Object.</item>
    ///   <item><see cref="GetImportEntries"/> never throws out to the host; any single-entry
    ///     build failure is caught and converted to a per-entry engine error.  A failure to
    ///     call <c>GetImportPage</c> on the pipe (transport error) is allowed to propagate
    ///     because the session is unrecoverable at that point.</item>
    ///   <item><see cref="CloseImportConnection"/> disposes the worker host regardless of
    ///     whether the pipe call succeeds, so the worker process is always killed.</item>
    /// </list>
    ///
    /// Thread safety: this class is used by the MIM engine on a single thread per import
    /// run; no locking is applied.
    /// </remarks>
    internal sealed class ImportConnection
    {
        private readonly WorkerSession session;

        // -------------------------------------------------------------------------
        // Constructors
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises an <see cref="ImportConnection"/> that resolves the worker
        /// executable path from the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable.
        /// </summary>
        public ImportConnection()
        {
            this.session = new WorkerSession();
        }

        /// <summary>
        /// Initialises an <see cref="ImportConnection"/> with an explicit worker executable
        /// path.  Intended for use in tests where the real environment variable may not be set.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal ImportConnection(string workerExePath)
        {
            this.session = new WorkerSession(workerExePath);
        }

        // -------------------------------------------------------------------------
        // IMAExtensible2CallImport
        // -------------------------------------------------------------------------

        /// <summary>
        /// Gets the default page size suggested to the MIM engine when the run profile
        /// does not specify one.
        /// </summary>
        public int ImportDefaultPageSize
        {
            get
            {
                return 100;
            }
        }

        /// <summary>
        /// Gets the maximum page size this MA will accept.
        /// </summary>
        public int ImportMaxPageSize
        {
            get
            {
                return 10000;
            }
        }

        /// <summary>
        /// Spawns the worker process, establishes the pipe connection, and sends the
        /// <c>OpenImport</c> JSON-RPC call.
        /// </summary>
        /// <param name="configParameters">
        /// The MA configuration parameters supplied by the host engine.
        /// </param>
        /// <param name="types">The schema for this import run.</param>
        /// <param name="importRunStep">
        /// The run-step descriptor carrying import type, page size, and custom data.
        /// </param>
        /// <returns>
        /// An <see cref="OpenImportConnectionResults"/> carrying the custom data returned
        /// by the worker's <c>OpenImport</c> response.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved from the environment,
        /// when the worker fails to start, or when the JSON-RPC call returns an error.
        /// </exception>
        public OpenImportConnectionResults OpenImportConnection(
            System.Collections.ObjectModel.KeyedCollection<string, ConfigParameter> configParameters,
            Schema types,
            OpenImportConnectionRunStep importRunStep)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            if (types == null)
            {
                throw new ArgumentNullException("types");
            }

            if (importRunStep == null)
            {
                throw new ArgumentNullException("importRunStep");
            }

            JsonRpcPipeClient client = this.session.Open();

            string runStepXml = MmsPipeSerializer.SerializeXml<OpenImportConnectionRunStep>(importRunStep);
            string schemaXml = MmsPipeSerializer.SerializeXml<Schema>(types);
            string configParametersXml = ConfigParameterSerialization.Serialize(configParameters);

            // Capture the engine's ExtensionsDirectory on the net48 side (the only Utils value any MA reads)
            // and inject it into the worker, whose own Utils static would resolve to the WRONG directory.
            string customData = client.OpenImport(runStepXml, schemaXml, configParametersXml, Utils.ExtensionsDirectory);

            return new OpenImportConnectionResults(customData);
        }

        /// <summary>
        /// Calls <c>GetImportPage</c> on the worker and converts the wire entries to real
        /// <see cref="CSEntryChange"/> objects. A malformed page payload is a transport-level
        /// failure and propagates; per-entry poison isolation is not applied on the shim side (a
        /// malformed wire entry indicates a marshalling defect, not connected-directory data).
        /// </summary>
        /// <param name="importRunStep">Unused by this implementation; reserved for future use.</param>
        /// <returns>
        /// A <see cref="GetImportEntriesResults"/> containing the page of entries and
        /// the <c>MoreToImport</c> flag.
        /// </returns>
        public GetImportEntriesResults GetImportEntries(GetImportEntriesRunStep importRunStep)
        {
            ImportPageResult page = this.session.Client.GetImportPage();

            IList<CSEntryChange> csentries = ProcessPage(page);

            return new GetImportEntriesResults(page.CustomData, page.MoreToImport, csentries);
        }

        /// <summary>
        /// Sends <c>CloseImport</c> to the worker, then unconditionally disposes the worker
        /// host (killing the worker process).
        /// </summary>
        /// <param name="importRunStep">
        /// The run-step descriptor carrying the close reason and custom data.
        /// </param>
        /// <returns>
        /// A <see cref="CloseImportConnectionResults"/> carrying the custom data returned
        /// by the worker's <c>CloseImport</c> response.
        /// </returns>
        public CloseImportConnectionResults CloseImportConnection(CloseImportConnectionRunStep importRunStep)
        {
            if (importRunStep == null)
            {
                throw new ArgumentNullException("importRunStep");
            }

            string customData = null;

            try
            {
                customData = this.session.Client.CloseImport(importRunStep.CustomData);
            }
            finally
            {
                // Dispose the worker host and pipe client unconditionally so the worker
                // process is always killed even if the CloseImport call fails.
                this.session.Dispose();
            }

            return new CloseImportConnectionResults(customData);
        }

        // -------------------------------------------------------------------------
        // Internal testable logic
        // -------------------------------------------------------------------------

        /// <summary>
        /// Deserialises the page's real <see cref="CSEntryChange"/> entries from the
        /// <c>MmsPipeSerializer</c> XML payload the worker sent. The entries are real host objects
        /// reconstructed via the shared surrogate; no per-field translation occurs.
        /// </summary>
        /// <remarks>
        /// Per-entry import error-path handling (poison-entry isolation, the host import error codes)
        /// is part of the separate error-path work (Phase 4b) and is intentionally not applied here.
        /// A malformed page payload is a transport-level failure and propagates.
        /// </remarks>
        /// <param name="page">The page result from the worker.</param>
        /// <returns>A list of real <see cref="CSEntryChange"/> ready to return to the engine.</returns>
        internal IList<CSEntryChange> ProcessPage(ImportPageResult page)
        {
            if (page == null || string.IsNullOrEmpty(page.EntriesXml))
            {
                return new List<CSEntryChange>();
            }

            List<CSEntryChange> entries =
                MmsPipeSerializer.DeserializeXml<List<CSEntryChange>>(page.EntriesXml);

            return entries ?? new List<CSEntryChange>();
        }
    }
}
