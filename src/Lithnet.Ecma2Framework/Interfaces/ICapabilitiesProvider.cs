using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines the methods and properties that a capabilities provider must implement
    /// </summary>
    public interface ICapabilitiesProvider
    {
        /// <summary>
        /// Gets the capabilities of the management agent
        /// </summary>
        /// <param name="configParameters">The configuration parameters of the management agent that have already been selected in the user interface, if the management agent chose to show a "Capabilities" configuration page</param>
        /// <returns>A MACapabilities object representing the capabilities of the management agent</returns>
        Task<MACapabilities> GetCapabilitiesAsync(IConfigParameters configParameters);
    }
}
