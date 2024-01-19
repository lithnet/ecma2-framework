using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.Configuration;

namespace Lithnet.Ecma2Framework
{
    internal class EcmaConfigurationProvider : ConfigurationProvider
    {
        private ConfigParameters configParameters;
        private IConfigRegistrationProvider mappingProvider;

        public EcmaConfigurationProvider(ConfigParameters configParameters, IConfigRegistrationProvider mappingProvider)
        {
            this.configParameters = configParameters;
            this.mappingProvider = mappingProvider;

            this.configParameters.ConfigParametersChanged += (_, _) => this.Load();
        }

        public override void Load()
        {
            foreach (var parameter in this.configParameters.Parameters)
            {
                var key = this.mappingProvider.GetKeyFromParameterName(parameter.Name);
                if (key == null)
                {
                    continue;
                }

                this.Set(key, parameter.IsEncrypted ? parameter.SecureValue.ConvertToUnsecureString() : parameter.Value);
            }

            this.OnReload();
        }
    }
}
