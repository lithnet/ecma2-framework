using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParameters
    {
        bool GetBool(string name, bool defaultValue);

        int GetInt(string name, int defaultValue);

        List<string> GetList(string name, string separator);

        long GetLong(string name, long defaultValue);

        public string GetString(string name);

        public string GetString(string name, string defaultValue);

        bool HasValue(string name);

        KeyedCollection<string, ConfigParameter> Parameters { get; }
    }
}
