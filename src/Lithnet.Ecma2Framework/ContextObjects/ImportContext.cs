using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class ImportContext
    {
        public ImportContext()
        {
            this.OutgoingWatermark = new WatermarkKeyedCollection();
        }

        public OpenImportConnectionRunStep RunStep { get; internal set; }

        public bool InDelta => this.RunStep?.ImportType == OperationType.Delta;

        public WatermarkKeyedCollection IncomingWatermark { get; internal set; }

        public WatermarkKeyedCollection OutgoingWatermark { get; internal set; }

        public Schema Types { get; internal set; }

        public CancellationToken Token => this.CancellationTokenSource.Token;

        public BlockingCollection<CSEntryChange> ImportItems { get; internal set; }

        public object CustomData { get; set; }

        internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        internal Stopwatch Timer { get; } = new Stopwatch();

        internal int ImportedItemCount;

        internal TimeSpan ProducerDuration { get; set; }

        internal Task Producer { get; set; }
    }
}
