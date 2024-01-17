using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ICapabilitiesProvider
    {
        Task GetConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        Task<ParameterValidationResult> ValidateConfigParametersAsync(IConfigParameters configParameters);

        Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters);
    }
}
