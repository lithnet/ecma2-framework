using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2
    {
        private readonly ILogger logger;
        private readonly IServiceProvider serviceProvider;
        private readonly IEcma2ConfigParameters configParameters;

        public Ecma2(Ecma2Initializer init)
        {
            this.serviceProvider = init.Build();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<Ecma2>>();
            this.configParameters = this.serviceProvider.GetRequiredService<IEcma2ConfigParameters>();
        }

        public async Task<Schema> GetSchemaAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                this.configParameters.SetConfigParameters(configParameters);

                SchemaContext context = new SchemaContext()
                {
                    ConfigParameters = configParameters,
                };

                var initializers = this.serviceProvider.GetServices<IOperationInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        this.logger.LogInformation("Launching initializer");
                        await initializer.InitializeSchemaOperationAsync(context);
                        this.logger.LogInformation("Initializer complete");
                    }
                }

                ISchemaProvider provider = this.serviceProvider.GetRequiredService<ISchemaProvider>();

                return await provider.GetMmsSchemaAsync(context);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Could not retrieve schema");
                throw;
            }
        }

        public async Task<MACapabilities> GetCapabilitiesExAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                this.configParameters.SetConfigParameters(configParameters);
                ICapabilitiesProvider provider = this.serviceProvider.GetRequiredService<ICapabilitiesProvider>();
                return await provider.GetCapabilitiesExAsync(configParameters);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Could not get capabilities");
                throw;
            }
        }

        public async Task<IList<ConfigParameterDefinition>> GetConfigParametersExAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                this.configParameters.SetConfigParameters(configParameters);

                var configParameterDefinitions = new List<ConfigParameterDefinition>();

                if (pageNumber == 1)
                {
                    //Logging.AddBuiltInLoggingParameters(page, configParameterDefinitions);
                }

                IConfigParametersProviderEx provider = this.serviceProvider.GetService<IConfigParametersProviderEx>();

                if (provider != null)
                {
                    await provider.GetConfigParametersExAsync(configParameters, configParameterDefinitions, page, pageNumber);
                }

                return configParameterDefinitions;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Could not get config parameters");
                throw;
            }
        }

        public async Task<ParameterValidationResult> ValidateConfigParametersAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                this.configParameters.SetConfigParameters(configParameters);

                IConfigParametersProviderEx provider = this.serviceProvider.GetService<IConfigParametersProviderEx>();

                if (provider != null)
                {
                    return await provider.ValidateConfigParametersExAsync(configParameters, page, pageNumber);
                }

                return new ParameterValidationResult();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Could not validate config parameters");
                throw;
            }
        }
    }
}