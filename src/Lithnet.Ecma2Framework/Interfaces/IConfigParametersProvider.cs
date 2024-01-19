using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IConfigParametersProvider
    {
        Task GetConnectivityConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        Task GetGlobalConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        Task GetRunStepConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        Task GetPartitionConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        Task GetCapabilitiesConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        Task GetSchemaConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions, int pageNumber);

        Task<ParameterValidationResult> ValidateSchemaConfigParametersAsync(IConfigParameters configParameters, int pageNumber);

        Task<ParameterValidationResult> ValidateCapabilitiesConfigParametersAsync(IConfigParameters configParameters);

        Task<ParameterValidationResult> ValidateConnectivityConfigParametersAsync(IConfigParameters configParameters);

        Task<ParameterValidationResult> ValidateGlobalConfigParametersAsync(IConfigParameters configParameters);

        Task<ParameterValidationResult> ValidateRunStepConfigParametersAsync(IConfigParameters configParameters);

        Task<ParameterValidationResult> ValidatePartitionConfigParametersAsync(IConfigParameters configParameters);
    }
}
