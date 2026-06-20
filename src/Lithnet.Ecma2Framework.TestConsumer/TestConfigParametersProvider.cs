using System.Collections.Generic;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    public sealed class TestConfigParametersProvider : IConfigParametersProvider
    {
        // Exercised by the manifest emitter tests. The Connectivity and Global pages are left empty
        // because existing orchestrator/integration tests assert a zero count for them; the RunStep
        // and Schema pages carry the test parameters instead.
        public const string RunStepStringParameterName = "Run step note";
        public const string RunStepStringValidation = "^.{0,64}$";
        public const string SchemaDropDownParameterName = "Object scope";
        public static readonly string[] SchemaDropDownValues = new string[] { "Users", "Groups", "All" };
        public const string SchemaDropDownDefault = "All";
        public const bool SchemaDropDownExtensible = false;

        private static ParameterValidationResult Success()
        {
            return new ParameterValidationResult(ParameterValidationResultCode.Success, string.Empty, string.Empty);
        }

        public Task GetConnectivityConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions)
        {
            return Task.CompletedTask;
        }

        public Task GetGlobalConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions)
        {
            return Task.CompletedTask;
        }

        public Task GetRunStepConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions)
        {
            newDefinitions.Add(ConfigParameterDefinition.CreateStringParameter(RunStepStringParameterName, RunStepStringValidation));
            return Task.CompletedTask;
        }

        public Task GetPartitionConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions)
        {
            return Task.CompletedTask;
        }

        public Task GetCapabilitiesConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions)
        {
            return Task.CompletedTask;
        }

        public Task GetSchemaConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions, int pageNumber)
        {
            // Only page 1 carries definitions; later pages return empty so the host stops paging.
            if (pageNumber == 1)
            {
                newDefinitions.Add(ConfigParameterDefinition.CreateDropDownParameter(
                    SchemaDropDownParameterName,
                    SchemaDropDownValues,
                    SchemaDropDownExtensible,
                    SchemaDropDownDefault));
            }

            return Task.CompletedTask;
        }

        public Task<ParameterValidationResult> ValidateSchemaConfigParametersAsync(IConfigParameters configParameters, int pageNumber)
        {
            return Task.FromResult(Success());
        }

        public Task<ParameterValidationResult> ValidateCapabilitiesConfigParametersAsync(IConfigParameters configParameters)
        {
            return Task.FromResult(Success());
        }

        public Task<ParameterValidationResult> ValidateConnectivityConfigParametersAsync(IConfigParameters configParameters)
        {
            return Task.FromResult(Success());
        }

        public Task<ParameterValidationResult> ValidateGlobalConfigParametersAsync(IConfigParameters configParameters)
        {
            return Task.FromResult(Success());
        }

        public Task<ParameterValidationResult> ValidateRunStepConfigParametersAsync(IConfigParameters configParameters)
        {
            return Task.FromResult(Success());
        }

        public Task<ParameterValidationResult> ValidatePartitionConfigParametersAsync(IConfigParameters configParameters)
        {
            return Task.FromResult(Success());
        }
    }
}
