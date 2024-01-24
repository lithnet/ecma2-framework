using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines the methods and properties that a bootstrapper must implement
    /// </summary>
    public interface IEcmaBootstrapper
    {
        /// <summary>
        /// This method is called when the ECMA2 framework is initializing. Use this method to configure the configuration builder.
        /// </summary>
        /// <param name="builder">The configuration builder to configure</param>
        void Configure(IConfigurationBuilder builder);

        /// <summary>
        /// This method is called when the ECMA2 framework is initializing. Use this method to register your custom services with the DI container.
        /// </summary>
        /// <param name="services">A collection of services to add your custom services to</param>
        /// <param name="configParameters">The configuration parameters as made available from the Microsoft Identity Management service</param>
        void SetupServices(IServiceCollection services, IConfigParameters configParameters);
    }
}
