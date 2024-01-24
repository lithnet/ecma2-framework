using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// An internal interface used to automatically map and generate configuration parameters from strongly-typed objects
    /// 
    /// This interface does not need to be implemented by the user
    /// </summary>
    public interface IConfigRegistrationProvider
    {
        /// <summary>
        /// Registers the discovered configuration options with the service provider
        /// </summary>
        /// <param name="services">The services collection</param>
        /// <param name="configuration">The applications pre-built configuration</param>
        void RegisterOptions(IServiceCollection services, IConfiguration configuration);

        /// <summary>
        /// Gets the options key from the MMS parameter name
        /// </summary>
        /// <param name="name">The MMS parameter name</param>
        /// <returns>The key to use when building the options</returns>
        string GetKeyFromParameterName(string name);

        /// <summary>
        /// Gets the type of option from the MMS parameter name
        /// </summary>
        /// <param name="name">The MMS parameter name</param>
        /// <returns>A string representing the type of option</returns>
        string GetTypeNameFromParameterName(string name);
    }
}
