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

                var initializers = this.ServiceProvider.GetServices<IContextInitializer>();

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
                return await provider.GetCapabilitiesAsync(this.ConfigParameters);
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Could not get capabilities");
                throw;
            }
        }

        public async Task<IList<ConfigParameterDefinition>> GetConfigParametersAsync(KeyedCollection<string, ConfigParameter> existingConfigParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                this.InitializeDIContainer(existingConfigParameters);

                var newConfigParameters = new List<ConfigParameterDefinition>();

                switch (page)
                {
                    case ConfigParameterPage.Capabilities:
                        ICapabilitiesProvider capabilityProvider = this.ServiceProvider.GetRequiredService<ICapabilitiesProvider>();
                        try
                        {
                            await capabilityProvider.GetConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Schema:
                        ISchemaProvider schemaProvider = this.ServiceProvider.GetRequiredService<ISchemaProvider>();
                        try
                        {
                            await schemaProvider.GetConfigParametersAsync(this.ConfigParameters, newConfigParameters, pageNumber);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Connectivity:
                        IConfigParametersProvider provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                await provider.GetConnectivityConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;

                    case ConfigParameterPage.Global:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                await provider.GetGlobalConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;

                    case ConfigParameterPage.RunStep:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                await provider.GetRunStepConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;

                    case ConfigParameterPage.Partition:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                await provider.GetPartitionConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;
                }

                return newConfigParameters;
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

                IConfigParametersProvider provider;
                ParameterValidationResult result = null;

                switch (page)
                {
                    case ConfigParameterPage.Capabilities:
                        ICapabilitiesProvider capabilityProvider = this.ServiceProvider.GetRequiredService<ICapabilitiesProvider>();
                        try
                        {
                            result = await capabilityProvider.ValidateConfigParametersAsync(this.ConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Schema:
                        ISchemaProvider schemaProvider = this.ServiceProvider.GetRequiredService<ISchemaProvider>();
                        try
                        {
                            result = await schemaProvider.ValidateConfigParametersAsync(this.ConfigParameters, pageNumber);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Connectivity:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                result = await provider.ValidateConnectivityConfigParametersAsync(this.ConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;

                    case ConfigParameterPage.Global:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                result = await provider.ValidateGlobalConfigParametersAsync(this.ConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;

                    case ConfigParameterPage.RunStep:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                result = await provider.ValidateRunStepConfigParametersAsync(this.ConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;

                    case ConfigParameterPage.Partition:
                        provider = this.ServiceProvider.GetService<IConfigParametersProvider>();
                        if (provider != null)
                        {
                            try
                            {
                                result = await provider.ValidatePartitionConfigParametersAsync(this.ConfigParameters);
                            }
                            catch (NotImplementedException) { }
                        }
                        break;
                }

                return result ?? new ParameterValidationResult();
            }
            catch (Exception ex)
            {
                this.Logger?.LogError(ex, "Could not validate config parameters");
                throw;
            }
        }
    }
}