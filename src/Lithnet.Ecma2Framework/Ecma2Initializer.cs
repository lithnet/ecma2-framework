using System;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Initializer
    {
        private IServiceProvider serviceProvider;

        private IConfigurationBuilder configBuilder;
        private readonly IConfigRegistrationProvider mappingProvider;

        public Ecma2Initializer(IEcmaBootstrapper bootstrapper, IConfigRegistrationProvider mappingProvider)
        {
            this.Bootstrapper = bootstrapper;
            this.mappingProvider = mappingProvider;
        }

        private void AddInternalServices()
        {
        }

        public IServiceCollection Services { get; private set; }

        public IEcmaBootstrapper Bootstrapper { get; }

        internal IServiceProvider Build(KeyedCollection<string, ConfigParameter> configParameters)
        {
            ConfigParameters config;

            if (this.serviceProvider == null)
            {
                this.Services = new ServiceCollection();
                this.configBuilder = new ConfigurationBuilder();

                config = new ConfigParameters(configParameters);
                this.Services.AddSingleton<IConfigParameters>(config);

                this.configBuilder.Add(new EcmaConfigurationSource(config, this.mappingProvider));

                this.Bootstrapper.Configure(this.configBuilder);

                var configRoot = this.configBuilder.Build();
                this.Services.AddSingleton<IConfiguration>(configRoot);
                this.mappingProvider.RegisterOptions(this.Services, configRoot);

                this.Bootstrapper.SetupServices(this.Services, config);

                this.AddInternalServices();
                this.serviceProvider = this.Services.BuildServiceProvider();
            }
            else
            {
                config = this.serviceProvider.GetRequiredService<IConfigParameters>() as ConfigParameters;
                config.Parameters = configParameters;
            }

            return this.serviceProvider;
        }
    }
}
