using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.Configuration;

namespace Lithnet.Ecma2Framework
{
    internal class EcmaConfigurationSource : IConfigurationSource
    {
        private readonly object lockObject = new object();
        private readonly ConfigParameters config;
        private readonly IConfigRegistrationProvider mappingProvider;
        private EcmaConfigurationProvider provider;

        public EcmaConfigurationSource(ConfigParameters config, IConfigRegistrationProvider mappingProvider)
        {
            this.config = config;
            this.mappingProvider = mappingProvider;
        }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            lock (this.lockObject)
            {
                this.provider ??= new EcmaConfigurationProvider(this.config, this.mappingProvider);
            }

            return this.provider;
        }
    }
}
