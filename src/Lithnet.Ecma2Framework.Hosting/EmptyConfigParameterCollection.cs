using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// An empty KeyedCollection of ConfigParameter used when no configuration parameters
    /// are available at worker startup (e.g. before the shim has supplied them via RPC).
    /// </summary>
    internal sealed class EmptyConfigParameterCollection : KeyedCollection<string, ConfigParameter>
    {
        protected override string GetKeyForItem(ConfigParameter item)
        {
            return item.Name;
        }
    }
}
