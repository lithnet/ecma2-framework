using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.Tests
{
    /// <summary>
    /// A no-op <see cref="IConfigRegistrationProvider"/> used in unit tests.
    /// No options mappings are registered; all mapping queries return null.
    /// </summary>
    internal sealed class TestConfigRegistrationProvider : IConfigRegistrationProvider
    {
        public void RegisterOptions(IServiceCollection services, IConfiguration configuration)
        {
            // No options types to register in the test scenario.
        }

        public string GetKeyFromParameterName(string name)
        {
            return null;
        }

        public string GetTypeNameFromParameterName(string name)
        {
            return null;
        }
    }
}
