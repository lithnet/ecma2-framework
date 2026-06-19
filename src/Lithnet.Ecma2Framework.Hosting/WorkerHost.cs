using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// Holds the consumer's IEcmaStartup and config-registration provider (both supplied at compile time by
    /// the generated host entry point), builds the DI container on demand, and exposes typed provider
    /// resolution. There is no reflection-based discovery: the generated host Main instantiates the consumer's
    /// startup and registration provider directly and passes them to <see cref="Create"/>.
    /// </summary>
    internal sealed class WorkerHost
    {
        private readonly IEcmaStartup startup;
        private readonly IConfigRegistrationProvider mappingProvider;
        private IServiceProvider serviceProvider;

        private WorkerHost(IEcmaStartup startup, IConfigRegistrationProvider mappingProvider)
        {
            this.startup = startup;
            this.mappingProvider = mappingProvider;
        }

        /// <summary>Gets the built service provider. Null until BuildContainer is called.</summary>
        public IServiceProvider Services
        {
            get
            {
                return this.serviceProvider;
            }
        }

        /// <summary>
        /// Constructs a WorkerHost directly from a supplied startup and config registration provider,
        /// without any reflection-based discovery. This is the path used by the generated worker entry
        /// point, where the consumer's startup and the generated registration provider are known at
        /// compile time and instantiated by the generated Main.
        /// </summary>
        /// <param name="startup">The consumer's IEcmaStartup implementation.</param>
        /// <param name="registrationProvider">The config registration provider to use when building the container.</param>
        /// <returns>An uninitialised WorkerHost ready for BuildContainer to be called.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either argument is null.</exception>
        public static WorkerHost Create(IEcmaStartup startup, IConfigRegistrationProvider registrationProvider)
        {
            if (startup == null)
            {
                throw new ArgumentNullException(nameof(startup));
            }

            if (registrationProvider == null)
            {
                throw new ArgumentNullException(nameof(registrationProvider));
            }

            return new WorkerHost(startup, registrationProvider);
        }

        /// <summary>
        /// Builds the DI container from the startup configuration and the supplied config parameters.
        /// The container is cached; subsequent calls return the cached provider after updating config.
        /// </summary>
        /// <param name="configParameters">The MA configuration parameters.</param>
        /// <returns>The built IServiceProvider.</returns>
        public IServiceProvider BuildContainer(KeyedCollection<string, ConfigParameter> configParameters)
        {
            Ecma2Initializer initializer = new Ecma2Initializer(this.startup, this.mappingProvider);
            this.serviceProvider = initializer.Build(configParameters);
            return this.serviceProvider;
        }

        /// <summary>Resolves the registered ISchemaProvider from the container.</summary>
        public ISchemaProvider GetSchemaProvider()
        {
            return this.serviceProvider.GetRequiredService<ISchemaProvider>();
        }

        /// <summary>Resolves all registered IObjectImportProvider instances from the container.</summary>
        public IEnumerable<IObjectImportProvider> GetImportProviders()
        {
            return this.serviceProvider.GetServices<IObjectImportProvider>();
        }

        /// <summary>Resolves all registered IObjectExportProvider instances from the container.</summary>
        public IEnumerable<IObjectExportProvider> GetExportProviders()
        {
            return this.serviceProvider.GetServices<IObjectExportProvider>();
        }

        /// <summary>Resolves all registered IObjectPasswordProvider instances from the container.</summary>
        public IEnumerable<IObjectPasswordProvider> GetPasswordProviders()
        {
            return this.serviceProvider.GetServices<IObjectPasswordProvider>();
        }

        /// <summary>Resolves the registered ICapabilitiesProvider from the container.</summary>
        public ICapabilitiesProvider GetCapabilitiesProvider()
        {
            return this.serviceProvider.GetService<ICapabilitiesProvider>();
        }

        /// <summary>Resolves the registered IConfigParametersProvider from the container, if any.</summary>
        public IConfigParametersProvider GetConfigParametersProvider()
        {
            return this.serviceProvider.GetService<IConfigParametersProvider>();
        }
    }
}
