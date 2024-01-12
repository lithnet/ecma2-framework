using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public async Task<Schema> GetSchemaAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                Logging.SetupLogger(configParameters);

                SchemaContext context = new SchemaContext()
                {
                    ConfigParameters = configParameters,
                };

                var connectionContextProvider = InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>();

                if (connectionContextProvider != null)
                {
                    context.ConnectionContext = await connectionContextProvider.GetConnectionContextAsync(configParameters, ConnectionContextOperationType.Schema);
                }

                ISchemaProvider provider = InterfaceManager.GetProviderOrThrow<ISchemaProvider>();

                return await provider.GetMmsSchemaAsync(context);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not retrieve schema");
                throw;
            }
        }

        public async Task<MACapabilities> GetCapabilitiesExAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                Logging.SetupLogger(configParameters);
                ICapabilitiesProvider provider = InterfaceManager.GetProviderOrThrow<ICapabilitiesProvider>();
                return await provider.GetCapabilitiesExAsync(configParameters);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not get capabilities");
                throw;
            }
        }

        public async Task<IList<ConfigParameterDefinition>> GetConfigParametersExAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                Logging.SetupLogger(configParameters);

                var configParameterDefinitions = new List<ConfigParameterDefinition>();

                if (pageNumber == 1)
                {
                    Logging.AddBuiltInLoggingParameters(page, configParameterDefinitions);
                }

                IConfigParametersProviderEx provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProviderEx>();

                if (provider != null)
                {
                    await provider.GetConfigParametersExAsync(configParameters, configParameterDefinitions, page, pageNumber);
                }

                return configParameterDefinitions;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not get config parameters");
                throw;
            }
        }

        public async Task<ParameterValidationResult> ValidateConfigParametersAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                IConfigParametersProviderEx provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProviderEx>();

                if (provider != null)
                {
                    return await provider.ValidateConfigParametersExAsync(configParameters, page, pageNumber);
                }

                return new ParameterValidationResult();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not validate config parameters");
                throw;
            }
        }
    }
}