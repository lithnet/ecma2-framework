using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Lithnet.Ecma2Framework;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    public sealed class TestConsumerStartup : IEcmaStartup
    {
        public void Configure(IConfigurationBuilder builder)
        {
            // No additional configuration sources needed.
        }

        public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
        {
            services.AddLogging();
            services.AddSingleton<ISchemaProvider, TestSchemaProvider>();
            services.AddSingleton<IObjectImportProvider, TestImportProvider>();
            services.AddSingleton<IObjectExportProvider, TestExportProvider>();
            services.AddSingleton<IObjectPasswordProvider, TestPasswordProviderImpl>();
            services.AddSingleton<ICapabilitiesProvider, TestCapabilitiesProvider>();
            services.AddSingleton<IConfigParametersProvider, TestConfigParametersProvider>();
        }
    }
}
