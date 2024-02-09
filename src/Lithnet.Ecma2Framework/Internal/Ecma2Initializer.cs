using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// The Ecma2Initializer class is responsible for building the service provider and configuration for the Ecma2 framework
    /// </summary>
    public class Ecma2Initializer
    {
        private IServiceProvider serviceProvider;

        private IConfigurationBuilder configBuilder;
        private readonly IConfigRegistrationProvider mappingProvider;

        /// <summary>
        /// Initializes a new instance of the Ecma2Initializer class
        /// </summary>
        /// <param name="startup">The user-created startup class</param>
        /// <param name="mappingProvider">The generated configuration registration provider</param>
        public Ecma2Initializer(IEcmaStartup startup, IConfigRegistrationProvider mappingProvider)
        {
            this.Startup = startup;
            this.mappingProvider = mappingProvider;
        }

        /// <summary>
        /// Gets the service collection that is used to build the service provider
        /// </summary>
        public IServiceCollection Services { get; private set; }

        /// <summary>
        /// Represents the user-provided startup class
        /// </summary>
        public IEcmaStartup Startup { get; }

        /// <summary>
        /// Builds the service provider and configuration
        /// </summary>
        /// <param name="configParameters">The raw configuration parameters provided by the synchronization service</param>
        /// <returns>A completed IServiceProvider</returns>
        internal IServiceProvider Build(KeyedCollection<string, ConfigParameter> configParameters)
        {
            ConfigParameters maConfig;

            if (this.serviceProvider == null)
            {
                this.Services = new ServiceCollection();
                this.Services.AddLogging();
                this.configBuilder = new ConfigurationBuilder();

                maConfig = new ConfigParameters(configParameters);
                this.Services.AddSingleton<IConfigParameters>(maConfig);

                this.configBuilder.Add(new EcmaConfigurationSource(maConfig, this.mappingProvider));

                this.Startup.Configure(this.configBuilder);

                var configRoot = this.configBuilder.Build();
                this.Services.AddSingleton<IConfiguration>(configRoot);
                this.mappingProvider.RegisterOptions(this.Services, configRoot);

                this.Startup.SetupServices(this.Services, maConfig);

                this.AddInternalServices(configRoot);
                this.serviceProvider = this.Services.BuildServiceProvider();
            }
            else
            {
                maConfig = this.serviceProvider.GetRequiredService<IConfigParameters>() as ConfigParameters;
                maConfig.Parameters = configParameters;
            }

            return this.serviceProvider;
        }


        /// <summary>
        /// Provides a hook for adding internal services to the service collection after the user has added their own services
        /// </summary>
        private void AddInternalServices(IConfiguration configuration)
        {
            this.Services.Configure<Ecma2FrameworkOptions>(configuration.GetSection("Ecma2Framework"));
        }
    }
}
