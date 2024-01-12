using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class ImportContext : IImportContext
    {
        public ImportContext()
        {
            this.OutgoingWatermark = new WatermarkKeyedCollection();
        }

        public bool InDelta => this.RunStep?.ImportType == OperationType.Delta;

        public KeyedCollection<string, ConfigParameter> ConfigParameters { get; internal set; }

        public WatermarkKeyedCollection IncomingWatermark { get; internal set; }

        public WatermarkKeyedCollection OutgoingWatermark { get; internal set; }

        public Schema Types { get; internal set; }

        internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public CancellationToken Token => this.CancellationTokenSource.Token;

        public BlockingCollection<CSEntryChange> ImportItems { get; internal set; }

        public object CustomData { get; set; }

        internal Stopwatch Timer { get; } = new Stopwatch();

        internal int ImportedItemCount;

        internal TimeSpan ProducerDuration { get; set; }

        internal Task Producer { get; set; }

        public OpenImportConnectionRunStep RunStep { get; internal set; }
    }
}
