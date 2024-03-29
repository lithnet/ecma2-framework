﻿using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lithnet.Ecma2Framework.Example
{
    internal class Startup : IEcmaStartup
    {
        /// <summary>
        /// Provides a method to configure the configuration builder. This allows you to add additional configuration sources
        /// </summary>
        /// <param name="builder">The configuration builder to configure</param>
        public void Configure(IConfigurationBuilder builder)
        {
        }

        /// <summary>
        /// Provides a method to configure the service collection. This allows you to add additional services to the service collection
        /// </summary>
        /// <param name="services">The service collection to configure</param>
        /// <param name="configParameters">The configuration parameters provided by the synchronization service</param>
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
