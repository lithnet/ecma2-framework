using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConnectionContextProvider
    {
        IConnectionContext GetConnectionContext(KeyedCollection<string, ConfigParameter> configParameters, ConnectionContextOperationType contextOperationType);
    }
}