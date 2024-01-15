using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Example
{
    internal class ConfigParametersProvider : IConfigParametersProviderEx
    {
        public const string TenantUrl = "TenantUrl";

        public Task GetConfigParametersExAsync(KeyedCollection<string, ConfigParameter> existingConfigParameters, IList<ConfigParameterDefinition> newDefinitions, ConfigParameterPage page, int pageNumber)
        {
            if (pageNumber != 1)
            {
                return Task.CompletedTask;
            }

            switch (page)
            {
                case ConfigParameterPage.Connectivity:
                    newDefinitions.Add(ConfigParameterDefinition.CreateStringParameter(TenantUrl, string.Empty));
                    newDefinitions.Add(ConfigParameterDefinition.CreateDividerParameter());
                    break;

                case ConfigParameterPage.Global:
                    break;

                case ConfigParameterPage.Partition:
                    break;

                case ConfigParameterPage.RunStep:
                    break;
            }

            return Task.CompletedTask;
        }

        public Task<ParameterValidationResult> ValidateConfigParametersExAsync(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return Task.FromResult(new ParameterValidationResult());
        }
    }
}
