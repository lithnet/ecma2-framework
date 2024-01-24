using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Represents a provider that can provide configuration parameters to the synchronization service
    /// </summary>
    public interface IConfigParametersProvider
    {
        /// <summary>
        /// Gets the configuration parameters that are displayed on the Connectivity page of the management agent configuration
        /// </summary>
        /// <param name="existingParameters">The set of configuration parameters and their values from configuration sections that have already been displayed and had their values set in the management agent configuration</param>
        /// <param name="newDefinitions">A list that new configuration parameters for this page can be added to</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetConnectivityConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        /// <summary>
        /// Gets the configuration parameters that are displayed on the Global page of the management agent configuration
        /// </summary>
        /// <param name="existingParameters">The set of configuration parameters and their values from configuration sections that have already been displayed and had their values set in the management agent configuration</param>
        /// <param name="newDefinitions">A list that new configuration parameters for this page can be added to</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetGlobalConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        /// <summary>
        /// Gets the configuration parameters that are displayed on the Run Step page of the management agent configuration
        /// </summary>
        /// <param name="existingParameters">The set of configuration parameters and their values from configuration sections that have already been displayed and had their values set in the management agent configuration</param>
        /// <param name="newDefinitions">A list that new configuration parameters for this page can be added to</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetRunStepConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);


        /// <summary>
        /// Gets the configuration parameters that are displayed on the Partition page of the management agent configuration
        /// </summary>
        /// <param name="existingParameters">The set of configuration parameters and their values from configuration sections that have already been displayed and had their values set in the management agent configuration</param>
        /// <param name="newDefinitions">A list that new configuration parameters for this page can be added to</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetPartitionConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        /// <summary>
        /// Gets the configuration parameters that are displayed on the Capabilities page of the management agent configuration. Note that the capabilities page is only shown when a management agent is first created.
        /// </summary>
        /// <param name="existingParameters">The set of configuration parameters and their values from configuration sections that have already been displayed and had their values set in the management agent configuration</param>
        /// <param name="newDefinitions">A list that new configuration parameters for this page can be added to</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetCapabilitiesConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions);

        /// <summary>
        /// Gets the configuration parameters that are displayed on the Schema page of the management agent configuration
        /// Management agents can display multiple schema pages, and the synchronization service will call this method on a loop, incrementing the page number each time, until no new items are returned.
        /// Make sure that you only return items for the requested page number, and that you return an empty list when the requested page number is greater than the number of pages you have to display.
        /// </summary>
        /// <param name="existingParameters">The set of configuration parameters and their values from configuration sections that have already been displayed and had their values set in the management agent configuration</param>
        /// <param name="newDefinitions">A list that new configuration parameters for this page can be added to</param>
        /// <param name="pageNumber">The page number that is being requested</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task GetSchemaConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions, int pageNumber);

        /// <summary>
        /// Validates the configuration parameters that are displayed on the Schema page of the management agent configuration.
        /// If there is no validation to perform, return a successful validation result.
        /// </summary>
        /// <param name="configParameters">The set of configuration parameters and their values from the Schema page of the management agent configuration</param>
        /// <param name="pageNumber">The page number that is being validated</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<ParameterValidationResult> ValidateSchemaConfigParametersAsync(IConfigParameters configParameters, int pageNumber);

        /// <summary>
        /// Validates the configuration parameters that are displayed on the Capabilities page of the management agent configuration.
        /// If there is no validation to perform, return a successful validation result.
        /// </summary>
        /// <param name="configParameters">The set of configuration parameters and their values from the Schema page of the management agent configuration</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<ParameterValidationResult> ValidateCapabilitiesConfigParametersAsync(IConfigParameters configParameters);

        /// <summary>
        /// Validates the configuration parameters that are displayed on the Connectivity page of the management agent configuration.
        /// If there is no validation to perform, return a successful validation result.
        /// </summary>
        /// <param name="configParameters">The set of configuration parameters and their values from the Schema page of the management agent configuration</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<ParameterValidationResult> ValidateConnectivityConfigParametersAsync(IConfigParameters configParameters);

        /// <summary>
        /// Validates the configuration parameters that are displayed on the Global page of the management agent configuration.
        /// If there is no validation to perform, return a successful validation result.
        /// </summary>
        /// <param name="configParameters">The set of configuration parameters and their values from the Schema page of the management agent configuration</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<ParameterValidationResult> ValidateGlobalConfigParametersAsync(IConfigParameters configParameters);


        /// <summary>
        /// Validates the configuration parameters that are displayed on the Run Step page of the management agent configuration.
        /// If there is no validation to perform, return a successful validation result.
        /// </summary>
        /// <param name="configParameters">The set of configuration parameters and their values from the Schema page of the management agent configuration</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<ParameterValidationResult> ValidateRunStepConfigParametersAsync(IConfigParameters configParameters);

        /// <summary>
        /// Validates the configuration parameters that are displayed on the Partition page of the management agent configuration.
        /// If there is no validation to perform, return a successful validation result.
        /// </summary>
        /// <param name="configParameters">The set of configuration parameters and their values from the Schema page of the management agent configuration</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<ParameterValidationResult> ValidatePartitionConfigParametersAsync(IConfigParameters configParameters);
    }
}
