using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.SourceGenDebugger
{
    internal class CapabilitiesProvider : ICapabilitiesProvider
    {
        public Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters)
        {
            throw new NotImplementedException();
        }

        public Task GetConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions)
        {
            throw new NotImplementedException();
        }

        public Task<ParameterValidationResult> ValidateConfigParametersAsync(IConfigParameters configParameters)
        {
            throw new NotImplementedException();
        }
    }
}
