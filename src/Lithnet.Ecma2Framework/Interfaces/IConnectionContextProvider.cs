using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConnectionContextProvider
    {
        Task<IConnectionContext> GetConnectionContextAsync(KeyedCollection<string, ConfigParameter> configParameters, ConnectionContextOperationType contextOperationType);
    }
}