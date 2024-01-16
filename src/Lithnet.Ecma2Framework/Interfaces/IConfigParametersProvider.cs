using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParametersProvider
    {
        Task GetConfigParametersAsync(KeyedCollection<string, ConfigParameter> existingConfigParameters, IList<ConfigParameterDefinition> newDefinitions, ConfigParameterPage page, int pageNumber);

        Task<ParameterValidationResult> ValidateConfigParametersAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber);
    }
}
