using System.Threading;

namespace Lithnet.Ecma2Framework
{
    public interface IExportContext : IConfigParameterContext
    {
        CancellationToken Token { get; }

        IConnectionContext ConnectionContext { get; }

        object CustomData { get; set; }
    }
}