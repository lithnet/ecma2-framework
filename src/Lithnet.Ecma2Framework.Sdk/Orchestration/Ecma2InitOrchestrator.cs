using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Orchestrates the stateless initialisation operations — schema retrieval, capability
    /// discovery, and configuration-parameter page dispatch — that the host calls before
    /// opening an import, export, or password session.
    /// </summary>
    /// <remarks>
    /// This orchestrator is intentionally stateless: each method resolves providers from
    /// the DI container, performs the work, and returns.  No session state is accumulated.
    ///
    /// Provider resolution:
    /// <list type="bullet">
    ///   <item><see cref="GetSchemaAsync"/> requires <see cref="ISchemaProvider"/>; throws
    ///     <see cref="InvalidOperationException"/> if not registered.</item>
    ///   <item><see cref="GetCapabilitiesAsync"/> requires <see cref="ICapabilitiesProvider"/>;
    ///     throws <see cref="InvalidOperationException"/> if not registered.</item>
    ///   <item><see cref="GetConfigParametersAsync"/> and
    ///     <see cref="ValidateConfigParametersAsync"/> resolve <see cref="IConfigParametersProvider"/>
    ///     optionally — if not registered, they return an empty list or a success result
    ///     respectively.  This mirrors the Ecma2.cs behaviour and allows management agents
    ///     that do not need custom config pages to omit the provider entirely.</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A null service provider argument throws
    ///     <see cref="ArgumentNullException"/>.</item>
    ///   <item>Provider exceptions are not caught here; they propagate to the caller (the
    ///     worker RPC handler) which surfaces them as JSON-RPC errors.</item>
    ///   <item><see cref="NotImplementedException"/> thrown by optional provider methods is
    ///     silently swallowed so the caller receives an empty/success result, preserving
    ///     the contract that providers may opt out of individual pages.</item>
    /// </list>
    /// </remarks>
    internal sealed class Ecma2InitOrchestrator
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<Ecma2InitOrchestrator> logger;

        /// <summary>
        /// Initialises a new <see cref="Ecma2InitOrchestrator"/> using the supplied service provider.
        /// </summary>
        /// <param name="serviceProvider">The DI container built for this connection.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="serviceProvider"/> is null.
        /// </exception>
        public Ecma2InitOrchestrator(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetService<ILogger<Ecma2InitOrchestrator>>();
        }

        /// <summary>
        /// Retrieves the management agent schema from the registered <see cref="ISchemaProvider"/>.
        /// </summary>
        /// <returns>The <see cref="Schema"/> returned by the provider.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no <see cref="ISchemaProvider"/> is registered in the container.
        /// </exception>
        public async Task<Schema> GetSchemaAsync()
        {
            this.LogInformation("Getting schema");

            ISchemaProvider provider = this.serviceProvider.GetRequiredService<ISchemaProvider>();

            Schema schema = await provider.GetMmsSchemaAsync();

            this.LogInformation("Schema retrieved: {0} type(s)", schema.Types.Count);

            return schema;
        }

        /// <summary>
        /// Retrieves the management agent capabilities from the registered
        /// <see cref="ICapabilitiesProvider"/>.
        /// </summary>
        /// <param name="configParameters">
        /// The existing configuration parameters from pages already displayed in the host UI.
        /// </param>
        /// <returns>The <see cref="MACapabilities"/> returned by the provider.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configParameters"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no <see cref="ICapabilitiesProvider"/> is registered in the container.
        /// </exception>
        public async Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            this.LogInformation("Getting capabilities");

            ICapabilitiesProvider provider = this.serviceProvider.GetRequiredService<ICapabilitiesProvider>();

            return await provider.GetCapabilitiesAsync(configParameters);
        }

        /// <summary>
        /// Returns the configuration parameter definitions for the given page by dispatching
        /// to the appropriate method on the registered <see cref="IConfigParametersProvider"/>.
        /// </summary>
        /// <param name="configParameters">The existing configuration parameters.</param>
        /// <param name="page">The page identifier.</param>
        /// <param name="pageNumber">The page number (used for multi-page Schema pages).</param>
        /// <returns>
        /// A list of <see cref="ConfigParameterDefinition"/> for the requested page.  Returns
        /// an empty list when no provider is registered or the provider throws
        /// <see cref="NotImplementedException"/> for this page.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configParameters"/> is null.
        /// </exception>
        public async Task<IList<ConfigParameterDefinition>> GetConfigParametersAsync(
            IConfigParameters configParameters,
            ConfigParameterPage page,
            int pageNumber)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            List<ConfigParameterDefinition> definitions = new List<ConfigParameterDefinition>();

            IConfigParametersProvider provider = this.serviceProvider.GetService<IConfigParametersProvider>();

            if (provider == null)
            {
                return definitions;
            }

            switch (page)
            {
                case ConfigParameterPage.Connectivity:
                {
                    try
                    {
                        await provider.GetConnectivityConfigParametersAsync(configParameters, definitions);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Global:
                {
                    try
                    {
                        await provider.GetGlobalConfigParametersAsync(configParameters, definitions);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.RunStep:
                {
                    try
                    {
                        await provider.GetRunStepConfigParametersAsync(configParameters, definitions);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Partition:
                {
                    try
                    {
                        await provider.GetPartitionConfigParametersAsync(configParameters, definitions);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Capabilities:
                {
                    try
                    {
                        await provider.GetCapabilitiesConfigParametersAsync(configParameters, definitions);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Schema:
                {
                    try
                    {
                        await provider.GetSchemaConfigParametersAsync(configParameters, definitions, pageNumber);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                default:
                {
                    this.LogInformation("GetConfigParametersAsync called with unrecognised page '{0}'; returning empty list.", page);
                    break;
                }
            }

            return definitions;
        }

        /// <summary>
        /// Validates the configuration parameters for the given page by dispatching to the
        /// appropriate method on the registered <see cref="IConfigParametersProvider"/>.
        /// </summary>
        /// <param name="configParameters">The configuration parameters to validate.</param>
        /// <param name="page">The page identifier.</param>
        /// <param name="pageNumber">The page number (used for multi-page Schema pages).</param>
        /// <returns>
        /// A <see cref="ParameterValidationResult"/> from the provider.  Returns a successful
        /// result when no provider is registered or the provider throws
        /// <see cref="NotImplementedException"/> for this page.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="configParameters"/> is null.
        /// </exception>
        public async Task<ParameterValidationResult> ValidateConfigParametersAsync(
            IConfigParameters configParameters,
            ConfigParameterPage page,
            int pageNumber)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            IConfigParametersProvider provider = this.serviceProvider.GetService<IConfigParametersProvider>();

            if (provider == null)
            {
                return new ParameterValidationResult(ParameterValidationResultCode.Success, string.Empty, string.Empty);
            }

            ParameterValidationResult result = null;

            switch (page)
            {
                case ConfigParameterPage.Connectivity:
                {
                    try
                    {
                        result = await provider.ValidateConnectivityConfigParametersAsync(configParameters);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Global:
                {
                    try
                    {
                        result = await provider.ValidateGlobalConfigParametersAsync(configParameters);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.RunStep:
                {
                    try
                    {
                        result = await provider.ValidateRunStepConfigParametersAsync(configParameters);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Partition:
                {
                    try
                    {
                        result = await provider.ValidatePartitionConfigParametersAsync(configParameters);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Capabilities:
                {
                    try
                    {
                        result = await provider.ValidateCapabilitiesConfigParametersAsync(configParameters);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                case ConfigParameterPage.Schema:
                {
                    try
                    {
                        result = await provider.ValidateSchemaConfigParametersAsync(configParameters, pageNumber);
                    }
                    catch (NotImplementedException) { }

                    break;
                }

                default:
                {
                    this.LogInformation("ValidateConfigParametersAsync called with unrecognised page '{0}'; returning success.", page);
                    break;
                }
            }

            return result ?? new ParameterValidationResult(ParameterValidationResultCode.Success, string.Empty, string.Empty);
        }

        // -------------------------------------------------------------------------
        // Logging helpers (guard against null logger)
        // -------------------------------------------------------------------------

        private void LogInformation(string message, params object[] args)
        {
            if (this.logger != null)
            {
                this.logger.LogInformation(message, args);
            }
        }
    }
}
