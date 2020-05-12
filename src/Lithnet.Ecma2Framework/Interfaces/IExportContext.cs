using System.Collections.ObjectModel;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IExportContext
    {
        KeyedCollection<string, ConfigParameter> ConfigParameters { get; }

        CancellationToken Token { get; }

        IConnectionContext ConnectionContext { get; }

        object CustomData { get; set; }
    }
}