using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IEcma2ConfigParameters
    {
        public string GetConfigParameter(string name);

        public string GetConfigParameter(string name, string defaultValue);

        internal void SetConfigParameters(KeyedCollection<string, ConfigParameter> parameters);
    }
}
