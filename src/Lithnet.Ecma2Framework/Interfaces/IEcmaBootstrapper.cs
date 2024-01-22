using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework
{
    public interface IEcmaBootstrapper
    {
        void Configure(IConfigurationBuilder builder);

        /// <summary>
        /// This method is called when the ECMA2 framework is initializing. Use this method to register your custom services with the DI container.
        /// </summary>
        /// <param name="services">A collection of services to add your custom services to</param>
        /// <param name="configParameters">The configuration parameters as made available from the Microsoft Identity Management service</param>
        void SetupServices(IServiceCollection services, IConfigParameters configParameters);
    }
}
