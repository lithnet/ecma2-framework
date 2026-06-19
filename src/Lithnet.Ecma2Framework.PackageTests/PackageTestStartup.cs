using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Lithnet.Ecma2Framework;

namespace Lithnet.Ecma2Framework.PackageTests
{
    /// <summary>
    /// The package-test consumer's <see cref="IEcmaStartup"/> implementation. It registers the minimal set of
    /// providers the worker-role generator requires for discovery (schema + capabilities + at least one
    /// import/export provider) so the generated host exe builds and runs end-to-end - driven ENTIRELY from the
    /// Lithnet.Ecma2Framework NuGet package (no framework source tree referenced).
    /// </summary>
    public sealed class PackageTestStartup : IEcmaStartup
    {
        public void Configure(IConfigurationBuilder builder)
        {
            // No additional configuration sources are needed for the package test.
        }

        public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
        {
            services.AddLogging();
            services.AddSingleton<ISchemaProvider, PackageTestSchemaProvider>();
            services.AddSingleton<ICapabilitiesProvider, PackageTestCapabilitiesProvider>();
            services.AddSingleton<IObjectImportProvider, PackageTestImportProvider>();
        }
    }
}
