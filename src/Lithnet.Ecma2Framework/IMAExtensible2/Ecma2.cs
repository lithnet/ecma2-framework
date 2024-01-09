using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2 :
        IMAExtensible2GetSchema,
        IMAExtensible2GetCapabilitiesEx,
        IMAExtensible2GetParametersEx,
        IMAExtensible2GetParameters
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public Schema GetSchema(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return AsyncHelper.RunSync(this.GetSchemaAsync(configParameters));
        }

        private async Task<Schema> GetSchemaAsync(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                Logging.SetupLogger(configParameters);
                SchemaContext context = new SchemaContext()
                {
                    ConfigParameters = configParameters,
                    ConnectionContext = await InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContextAsync(configParameters, ConnectionContextOperationType.Schema)
                };

                ISchemaProvider provider = InterfaceManager.GetProviderOrThrow<ISchemaProvider>();

                return await provider.GetMmsSchemaAsync(context);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not retrieve schema");
                throw;
            }
        }

        public IList<ConfigParameterDefinition> GetConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return this.GetConfigParametersEx(configParameters, page, 1);
        }

        public ParameterValidationResult ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return this.ValidateConfigParametersEx(configParameters, page, 1);
        }

        public MACapabilities GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                Logging.SetupLogger(configParameters);
                ICapabilitiesProvider provider = InterfaceManager.GetProviderOrThrow<ICapabilitiesProvider>();
                return AsyncHelper.RunSync(provider.GetCapabilitiesExAsync(configParameters));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not get capabilities");
                throw;
            }
        }

        public IList<ConfigParameterDefinition> GetConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
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
                    AsyncHelper.RunSync(provider.GetConfigParametersExAsync(configParameters, configParameterDefinitions, page, pageNumber));
                }

                return configParameterDefinitions;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Could not get config parameters");
                throw;

            }
        }

        public ParameterValidationResult ValidateConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            try
            {
                IConfigParametersProviderEx provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProviderEx>();

                if (provider != null)
                {
                    return AsyncHelper.RunSync(provider.ValidateConfigParametersExAsync(configParameters, page, pageNumber));
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