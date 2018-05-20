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
                IConfigParametersProvider provider = InterfaceManager.GetProviderOrDefault<IConfigParametersProvider>();
                return provider?.GetConfigParameters(configParameters, page) ?? new List<ConfigParameterDefinition>();
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
                ICapabilitiesProvider provider = InterfaceManager.GetProviderOrThrow<ICapabilitiesProvider>();
                return provider.GetCapabilitiesEx(configParameters);
            }
            catch (Exception ex)
            {
                logger.Error(ex.UnwrapIfSingleAggregateException(), "Could not get capabilities");
                throw;
            }
        }
    }
}