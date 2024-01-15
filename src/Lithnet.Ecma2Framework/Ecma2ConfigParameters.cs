using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2ConfigParameters : IEcma2ConfigParameters
    {
        public KeyedCollection<string, ConfigParameter> Parameters { get; private set; }

        public string GetConfigParameter(string name)
        {
            return this.GetConfigParameter(name, null);
        }

        public string GetConfigParameter(string name, string defaultValue)
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

        void IEcma2ConfigParameters.SetConfigParameters(KeyedCollection<string, ConfigParameter> parameters)
        {
            this.Parameters = parameters;
        }
    }
}
