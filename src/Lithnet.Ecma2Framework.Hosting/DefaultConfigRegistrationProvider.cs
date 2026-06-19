using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// A no-op <see cref="IConfigRegistrationProvider"/> for consumers that declare no options/configuration
    /// classes requiring parameter-name mapping. A production consumer with such classes receives the
    /// generated <c>Ecma2ConfigRegistrationProvider</c> from the source generator instead; this default is
    /// for the no-mapping case (used by the TestConsumer worker).
    /// </summary>
    public sealed class DefaultConfigRegistrationProvider : IConfigRegistrationProvider
    {
        public void RegisterOptions(IServiceCollection services, IConfiguration configuration)
        {
            // No options types to register in the default (no-op) scenario.
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
