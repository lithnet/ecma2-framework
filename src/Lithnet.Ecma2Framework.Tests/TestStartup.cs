using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.Tests
{
    /// <summary>
    /// A minimal <see cref="IEcmaStartup"/> used in unit tests.
    /// Registers <see cref="TestSchemaProvider"/> and <see cref="TestImportProvider"/> with the DI container.
    /// </summary>
    internal sealed class TestStartup : IEcmaStartup
    {
        public void Configure(IConfigurationBuilder builder)
        {
            // No additional configuration sources needed in the test scenario.
        }

        public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
        {
            services.AddSingleton<ISchemaProvider, TestSchemaProvider>();
            services.AddSingleton<IObjectImportProvider, TestImportProvider>();
        }
    }
}
