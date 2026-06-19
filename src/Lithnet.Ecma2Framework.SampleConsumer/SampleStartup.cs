using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Lithnet.Ecma2Framework;

namespace Lithnet.Ecma2Framework.SampleConsumer
{
    /// <summary>
    /// The sample consumer's <see cref="IEcmaStartup"/> implementation. It registers the minimal set of
    /// providers the worker-role generator requires for discovery (schema + capabilities + at least one
    /// import/export provider) so the generated worker entry point builds and runs end-to-end.
    /// </summary>
    public sealed class SampleStartup : IEcmaStartup
    {
        public void Configure(IConfigurationBuilder builder)
        {
            // No additional configuration sources are needed for the sample.
        }

        public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
        {
            services.AddLogging();
            services.AddSingleton<ISchemaProvider, SampleSchemaProvider>();
            services.AddSingleton<ICapabilitiesProvider, SampleCapabilitiesProvider>();
            services.AddSingleton<IObjectImportProvider, SampleImportProvider>();
            services.AddSingleton<IConfigParametersProvider, SampleConfigParametersProvider>();
        }
    }
}
