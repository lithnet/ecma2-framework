using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class PasswordContext
    {
        public object ConnectionContext { get; internal set; }

        public KeyedCollection<string, ConfigParameter> ConfigParameters { get; internal set; }
    }
}
