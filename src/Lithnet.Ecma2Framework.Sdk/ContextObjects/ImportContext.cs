using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// The import context contains information about the currently running import operation
    /// </summary>
    public class ImportContext
    {
        /// <summary>
        /// Creates a new instance of the ImportContext class
        /// </summary>
        /// <param name="runStep">The run step that is currently executing</param>
        /// <param name="types">The schema of the management agent</param>
        internal ImportContext(OpenImportConnectionRunStep runStep, Schema types)
        {
            this.OutgoingWatermark = new ConcurrentDictionary<string, string>();
            this.ImportItems = new BlockingCollection<CSEntryChange>();
            this.CancellationTokenSource = new CancellationTokenSource();
            this.ImportCollectionWrapper = new CSEntryChangeBlockingCollectionWrapper(this.ImportItems, this.CancellationTokenSource.Token);
            this.RunStep = runStep;
            this.Types = types;
        }

        /// <summary>
        /// Creates an <see cref="ImportContext"/> from a worker-side run step and schema.
        /// This factory is the intended entry point for the worker host; it initialises all
        /// internal collections so the orchestrator can begin producing entries immediately.
        /// The <see cref="IncomingWatermark"/> is left as an empty dictionary and should be
        /// populated by the caller after deserialisng the inbound watermark JSON.
        /// </summary>
        /// <param name="runStep">The mirror run step built from the wire request.</param>
        /// <param name="types">The mirror schema built from the wire request.</param>
        /// <returns>A fully initialised <see cref="ImportContext"/> ready for use by the orchestrator.</returns>
        public static ImportContext Create(OpenImportConnectionRunStep runStep, Schema types)
        {
            ImportContext ctx = new ImportContext(runStep, types);
            ctx.IncomingWatermark = new ConcurrentDictionary<string, string>();
            return ctx;
        }

        /// <summary>
        /// Gets information about the run step that is currently executing
        /// </summary>
        public OpenImportConnectionRunStep RunStep { get; }

        /// <summary>
        /// Gets a value that indicates if the import operation is a delta import
        /// </summary>
        public bool InDelta => this.RunStep?.ImportType == OperationType.Delta;

        /// <summary>
        /// Gets the current schema of the management agent
        /// </summary>
        public Schema Types { get; }

        /// <summary>
        /// Gets or sets an object that can be used to store user-defined custom data that is shared by all import providers
        /// </summary>
        public object CustomData { get; set; }

        /// <summary>
        /// Gets the watermark collection to be returned to the synchronization service at the completion of the import operation
        /// </summary>
        internal ConcurrentDictionary<string, string> OutgoingWatermark { get; }

        /// <summary>
        /// Gets the incoming watermark collection provided by the synchronization service
        /// </summary>
        internal ConcurrentDictionary<string, string> IncomingWatermark { get; set; }

        /// <summary>
        /// Gets the internal collection of CSEntryChange objects that are to be imported
        /// </summary>
        internal BlockingCollection<CSEntryChange> ImportItems { get; }

        /// <summary>
        /// Gets a wrapper around the internal collection of CSEntryChange objects that are to be imported
        /// </summary>
        internal ICSEntryChangeCollection ImportCollectionWrapper { get; }

        /// <summary>
        /// Gets the internal cancellation source for the publicly exposed cancellation token
        /// </summary>
        internal CancellationTokenSource CancellationTokenSource { get; }

        /// <summary>
        /// Gets the internal timer used to time import operations
        /// </summary>
        internal Stopwatch Timer { get; } = new Stopwatch();

        /// <summary>
        /// Gets a field that keeps track of the imported item count
        /// </summary>
        internal int ImportedItemCount;

        /// <summary>
        /// Gets the time that the producer thread took to execute
        /// </summary>
        internal TimeSpan ProducerDuration { get; set; }

        /// <summary>
        /// Gets the task that represents the producer thread
        /// </summary>
        internal Task Producer { get; set; }
    }
}
