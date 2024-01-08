using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParametersProviderEx
    {
        Task GetConfigParametersExAsync(KeyedCollection<string, ConfigParameter> configParameters, IList<ConfigParameterDefinition> newDefinitions, ConfigParameterPage page, int pageNumber);

        Task<ParameterValidationResult> ValidateConfigParametersExAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber);
    }
}
