using System;
using System.Diagnostics;
using System.Threading;

namespace Lithnet.Ecma2Framework
{
    public class ExportContext
    {
        internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public CancellationToken Token => this.CancellationTokenSource.Token;

        public int ExportThreads { get; set; } = Environment.ProcessorCount * 2;

        public object CustomData { get; set; }

        internal Stopwatch Timer { get; } = new Stopwatch();

        internal int ExportedItemCount;
    }
}
