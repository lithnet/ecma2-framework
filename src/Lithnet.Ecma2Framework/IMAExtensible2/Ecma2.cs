using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            try
            {
                Logging.SetupLogger(configParameters);
                SchemaContext context = new SchemaContext()
                {
                    ConfigParameters = configParameters,
                    ConnectionContext = InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContext(configParameters, ConnectionContextOperationType.Schema)
                };

                ISchemaProvider provider = InterfaceManager.GetProviderOrThrow<ISchemaProvider>();

                return provider.GetMmsSchema(context);
            }
            catch (Exception ex)
            {
                logger.Error(ex.UnwrapIfSingleAggregateException(), "Could not retrieve schema");
                throw;
            }
        }

        public IList<ConfigParameterDefinition> GetConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            try
            {
                Logging.SetupLogger(configParameters);

                var configParameterDefinitions = new List<ConfigParameterDefinition>();
                Logging.AddBuiltInLoggingParameters(page, configParameterDefinitions);
                IConfigParametersProvider provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProvider>();

                if (provider == null)
                {
                    var providerEx = InterfaceManager.GetProviderOrDefault<IConfigParametersProviderEx>();
                    providerEx?.GetConfigParametersEx(configParameters, configParameterDefinitions, page, 1);
                }
                else
                {
                    provider.GetConfigParameters(configParameters, configParameterDefinitions, page);
                }

                return configParameterDefinitions;
            }
            catch (Exception ex)
            {
                logger.Error(ex.UnwrapIfSingleAggregateException(), "Could not get config parameters");
                throw;
            }
        }

        public ParameterValidationResult ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            try
            {
                var result = Logging.ValidateBuiltInLoggingParameters(configParameters, page);

                if (result != null)
                {
                    return result;
                }

                IConfigParametersProvider provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProvider>();
                return provider?.ValidateConfigParameters(configParameters, page) ?? new ParameterValidationResult();
            }
            catch (Exception ex)
            {
                logger.Error(ex.UnwrapIfSingleAggregateException(), "Could not validate config parameters");
                throw;
            }
        }

        public MACapabilities GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters)
        {
            try
            {
                Logging.SetupLogger(configParameters);
                ICapabilitiesProvider provider = InterfaceManager.GetProviderOrThrow<ICapabilitiesProvider>();
                return provider.GetCapabilitiesEx(configParameters);
            }
            catch (Exception ex)
            {
                logger.Error(ex.UnwrapIfSingleAggregateException(), "Could not get capabilities");
                throw;
            }
        }

        public IList<ConfigParameterDefinition> GetConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            IConfigParametersProviderEx provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProviderEx>();

            if (provider == null)
            {
                if (pageNumber > 1)
                {
                    return null;
                }

                return this.GetConfigParameters(configParameters, page);
            }

            var configParameterDefinitions = new List<ConfigParameterDefinition>();

            if (pageNumber == 1)
            {
                Logging.AddBuiltInLoggingParameters(page, configParameterDefinitions);
            }

            provider.GetConfigParametersEx(configParameters, configParameterDefinitions, page, pageNumber);

            return configParameterDefinitions;
        }

        public ParameterValidationResult ValidateConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            IConfigParametersProviderEx provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProviderEx>();

            if (provider == null)
            {
                if (pageNumber > 1)
                {
                    return null;
                }

                return this.ValidateConfigParameters(configParameters, page);
            }

            return provider.ValidateConfigParametersEx(configParameters, page, pageNumber);
        }
    }
}