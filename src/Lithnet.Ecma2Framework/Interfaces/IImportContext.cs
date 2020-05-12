using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IImportContext
    {
        bool InDelta { get; }

        KeyedCollection<string, ConfigParameter> ConfigParameters { get; }

        WatermarkKeyedCollection IncomingWatermark { get; }

        WatermarkKeyedCollection OutgoingWatermark { get; }

        Schema Types { get; }

        CancellationToken Token { get; }

        BlockingCollection<CSEntryChange> ImportItems { get; }

        IConnectionContext ConnectionContext { get; }

        object CustomData { get; set; }

        OpenImportConnectionRunStep RunStep { get; }
    }
}