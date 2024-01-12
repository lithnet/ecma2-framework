using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class ExportContext : IExportContext
    {
        public KeyedCollection<string, ConfigParameter> ConfigParameters { get; internal set; }

        internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        public CancellationToken Token => this.CancellationTokenSource.Token;

        public object CustomData { get; set; }

        internal Stopwatch Timer { get; } = new Stopwatch();

        internal int ExportedItemCount;
    }
}
