using System.Diagnostics;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// The export context contains information about the currently running export operation 
    /// </summary>
    public class ExportContext
    {
        /// <summary>
        /// Creates a new instance of the ExportContext class
        /// </summary>
        /// <param name="runStep">The run step that is currently executing</param>
        /// <param name="types">The schema of the management agent</param>
        internal ExportContext(OpenExportConnectionRunStep runStep, Schema types)
        {
            this.RunStep = runStep;
            this.Types = types;
        }

        /// <summary>
        /// Gets information about the run step that is currently executing
        /// </summary>
        public OpenExportConnectionRunStep RunStep { get; }

        /// <summary>
        /// Gets the current schema of the management agent
        /// </summary>
        public Schema Types { get; }

        /// <summary>
        /// Gets or sets an object that can be used to store user-defined custom data that is shared by all export providers
        /// </summary>
        public object CustomData { get; set; }

        /// <summary>
        /// Gets the internal timer used to time export operations
        /// </summary>
        internal Stopwatch Timer { get; } = new Stopwatch();

        /// <summary>
        /// Gets a field that keeps track of the exported item count
        /// </summary>
        internal int ExportedItemCount;

        /// <summary>
        /// The internal cancellation source for the publicly exposed cancellation token
        /// </summary>
        internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();
    }
}
