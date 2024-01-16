using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2ConfigParameters : IConfigParameters
    {
        public Ecma2ConfigParameters()
        {
        }

        public Ecma2ConfigParameters(KeyedCollection<string, ConfigParameter> parameters)
        {
            this.Parameters = parameters;
        }

        public KeyedCollection<string, ConfigParameter> Parameters { get; }

        public bool HasValue(string name)
        {
            if (this.Parameters == null)
            {
                return false;
            }

            return this.Parameters.Contains(name);
        }

        public string GetString(string name)
        {
            return this.GetString(name, null);
        }

        public string GetString(string name, string defaultValue)
        {
            if (this.Parameters == null)
            {
                return defaultValue;
            }

            if (this.Parameters.Contains(name))
            {
                if (this.Parameters[name].IsEncrypted)
                {
                    return this.Parameters[name].SecureValue?.ConvertToUnsecureString();
                }
                else
                {
                    return this.Parameters[name].Value;
                }
            }
            else
            {
                return defaultValue;
            }
        }

        public bool GetBool(string name, bool defaultValue)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            else
            {
                return defaultValue;
            }
        }

        public int GetInt(string name, int defaultValue)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (int.TryParse(value, out int result))
            {
                return result;
            }
            else
            {
                return defaultValue;
            }
        }

        public long GetLong(string name, long defaultValue)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (long.TryParse(value, out long result))
            {
                return result;
            }
            else
            {
                return defaultValue;
            }
        }

        public List<string> GetList(string name, string separator)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return new List<string>(value.Split(new string[] { separator }, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
