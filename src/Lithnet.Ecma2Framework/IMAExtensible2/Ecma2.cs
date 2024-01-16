using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2 : Ecma2Base
    {
        public Ecma2(Ecma2Initializer initializer) : base(initializer)
        {
        }

        public async Task<Schema> GetSchemaAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                this.InitializeDIContainer(configParameters);

                SchemaContext context = new SchemaContext();

                var initializers = this.ServiceProvider.GetServices<IOperationInitializer>();

                if (initializers != null)
                {
                    foreach (var initializer in initializers)
                    {
                        this.Logger.LogInformation("Launching initializer");
                        await initializer.InitializeSchemaOperationAsync(context);
                        this.Logger.LogInformation("Initializer complete");
                    }
                }

                ISchemaProvider provider = this.ServiceProvider.GetRequiredService<ISchemaProvider>();

                return await provider.GetMmsSchemaAsync(context);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Could not retrieve schema");
                throw;
            }
        }

        public async Task<MACapabilities> GetCapabilitiesAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                this.InitializeDIContainer(configParameters);

                ICapabilitiesProvider provider = this.ServiceProvider.GetRequiredService<ICapabilitiesProvider>();
                return await provider.GetCapabilitiesAsync(configParameters);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Could not get capabilities");
                throw;
            }
        }

        public async Task<IList<ConfigParameterDefinition>> GetConfigParametersAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                this.InitializeDIContainer(configParameters);

                var configParameterDefinitions = new List<ConfigParameterDefinition>();

                IConfigParametersProvider provider = this.ServiceProvider.GetService<IConfigParametersProvider>();

                if (provider != null)
                {
                    await provider.GetConfigParametersAsync(configParameters, configParameterDefinitions, page, pageNumber);
                }

                return configParameterDefinitions;
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Could not get config parameters");
                throw;
            }
        }

        public async Task<ParameterValidationResult> ValidateConfigParametersAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                this.InitializeDIContainer(configParameters);

                IConfigParametersProvider provider = this.ServiceProvider.GetService<IConfigParametersProvider>();

                if (provider != null)
                {
                    return await provider.ValidateConfigParametersAsync(configParameters, page, pageNumber);
                }

                return new ParameterValidationResult();
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Could not validate config parameters");
                throw;
            }
        }
    }
}