using System.Collections.Concurrent;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IImportContext : IConfigParameterContext
    {
        bool InDelta { get; }

        WatermarkKeyedCollection IncomingWatermark { get; }

        WatermarkKeyedCollection OutgoingWatermark { get; }

        Schema Types { get; }

        CancellationToken Token { get; }

        BlockingCollection<CSEntryChange> ImportItems { get; }

        object CustomData { get; set; }

        OpenImportConnectionRunStep RunStep { get; }
    }
}