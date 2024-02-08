using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lithnet.Ecma2Framework.Example
{
    internal class Startup : IEcmaStartup
    {
        public void Configure(IConfigurationBuilder builder)
        {
        }

        public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
        {
            services.AddSingleton<ICapabilitiesProvider, CapabilitiesProvider>();
            services.AddSingleton<IObjectImportProvider, UserImportProvider>();
            services.AddSingleton<ISchemaProvider, SchemaProvider>();

            services.AddSingleton<HttpClient>((services) =>
            {
                var options = services.GetRequiredService<IOptions<ConnectivityOptions>>();

                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri(options.Value.ApiUrl);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                return client;
            });
        }
    }
}
