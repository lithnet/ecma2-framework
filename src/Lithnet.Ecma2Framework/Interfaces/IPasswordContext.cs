using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IPasswordContext : IConfigParameterContext
    {
        IConnectionContext ConnectionContext { get; }
    }
}