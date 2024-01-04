using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParametersProvider
    {
        void GetConfigParameters(KeyedCollection<string, ConfigParameter> existingConfigParameters, IList<ConfigParameterDefinition> newDefinitions, ConfigParameterPage page);

        ParameterValidationResult ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page);
    }
}
