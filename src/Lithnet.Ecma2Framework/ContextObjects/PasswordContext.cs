using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class PasswordContext : IPasswordContext
    {
        public KeyedCollection<string, ConfigParameter> ConfigParameters { get; internal set; }

        public object CustomData { get; set; }
    }
}
