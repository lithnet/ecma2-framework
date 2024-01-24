using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// <para>A class responsible for providing the working implementation of the IMAExtensible2 interfaces related to configuration, schema, and capabilities</para>
    /// <para>This class is called by generated code, and should not be called directly</para>
    /// </summary>
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

                ISchemaProvider provider = this.ServiceProvider.GetRequiredService<ISchemaProvider>();

                return await provider.GetMmsSchemaAsync();
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
                IConfigParametersProvider provider = this.ServiceProvider.GetService<IConfigParametersProvider>();

                if (provider == null)
                {
                    return newConfigParameters;
                }

                switch (page)
                {
                    case ConfigParameterPage.Capabilities:
                        try
                        {
                            await provider.GetCapabilitiesConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Schema:
                        try
                        {
                            await provider.GetSchemaConfigParametersAsync(this.ConfigParameters, newConfigParameters, pageNumber);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Connectivity:
                        try
                        {
                            await provider.GetConnectivityConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Global:
                        try
                        {
                            await provider.GetGlobalConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.RunStep:
                        try
                        {
                            await provider.GetRunStepConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Partition:
                        try
                        {
                            await provider.GetPartitionConfigParametersAsync(this.ConfigParameters, newConfigParameters);
                        }
                        catch (NotImplementedException) { }
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

                IConfigParametersProvider provider = this.ServiceProvider.GetService<IConfigParametersProvider>();

                ParameterValidationResult result = null;

                if (provider == null)
                {
                    return new ParameterValidationResult();
                }

                switch (page)
                {
                    case ConfigParameterPage.Capabilities:
                        try
                        {
                            result = await provider.ValidateCapabilitiesConfigParametersAsync(this.ConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Schema:
                        try
                        {
                            result = await provider.ValidateSchemaConfigParametersAsync(this.ConfigParameters, pageNumber);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Connectivity:

                        try
                        {
                            result = await provider.ValidateConnectivityConfigParametersAsync(this.ConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Global:
                        try
                        {
                            result = await provider.ValidateGlobalConfigParametersAsync(this.ConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.RunStep:
                        try
                        {
                            result = await provider.ValidateRunStepConfigParametersAsync(this.ConfigParameters);
                        }
                        catch (NotImplementedException) { }
                        break;

                    case ConfigParameterPage.Partition:
                        try
                        {
                            result = await provider.ValidatePartitionConfigParametersAsync(this.ConfigParameters);
                        }
                        catch (NotImplementedException) { }
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