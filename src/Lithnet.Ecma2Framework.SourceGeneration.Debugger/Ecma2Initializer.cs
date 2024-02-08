using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.SourceGenDebugger
{
    internal class Ecma2Initializer : IEcmaStartup
    {
        public void Configure(IConfigurationBuilder builder)
        {
        }

        public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
        {
        }
    }
}
