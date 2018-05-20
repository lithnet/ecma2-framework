using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class ExportContext
    {
        public KeyedCollection<string, ConfigParameter> ConfigParameters { get; internal set; }

        public CancellationTokenSource CancellationTokenSource { get; internal set; }

        public object ConnectionContext { get; internal set; }

        internal Stopwatch Timer { get; } = new Stopwatch();

        internal int ExportedItemCount;
    }
}
