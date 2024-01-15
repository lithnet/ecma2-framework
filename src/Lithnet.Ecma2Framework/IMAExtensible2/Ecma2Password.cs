using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Password
    {
        private readonly ILogger logger;
        private readonly IServiceProvider serviceProvider;
        private readonly IEcma2ConfigParameters configParameters;

        private List<IObjectPasswordProvider> providerCache;
        private PasswordContext passwordContext;

        public Ecma2Password(Ecma2Initializer init)
        {
            this.serviceProvider = init.Build();
            this.logger = this.serviceProvider.GetRequiredService<ILogger<Ecma2Password>>();
            this.configParameters = this.serviceProvider.GetRequiredService<IEcma2ConfigParameters>();
        }

        private List<IObjectPasswordProvider> Providers
        {
            get
            {
                this.providerCache ??= this.serviceProvider.GetServices<IObjectPasswordProvider>().ToList();
                return this.providerCache;
            }
        }

        public Task<ConnectionSecurityLevel> GetConnectionSecurityLevelAsync()
        {
            return Task.FromResult(ConnectionSecurityLevel.Secure);
        }

        public async Task OpenPasswordConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            this.configParameters.SetConfigParameters(configParameters);

            this.passwordContext = new PasswordContext()
            {
                ConfigParameters = configParameters
            };

            var initializers = this.serviceProvider.GetServices<IOperationInitializer>();

            if (initializers != null)
            {
                foreach (var initializer in initializers)
                {
                    this.logger.LogInformation("Launching initializer");
                    await initializer.InitializePasswordOperationAsync(this.passwordContext);
                    this.logger.LogInformation("Initializer complete");
                }
            }

            await this.InitializeProvidersAsync(this.passwordContext);
        }


        public Task ClosePasswordConnectionAsync()
        {
            return Task.CompletedTask;
        }


        public async Task SetPasswordAsync(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            try
            {
                this.logger.LogTrace($"Setting password for: {csentry.DN}");
                IObjectPasswordProvider provider = await this.GetProviderForTypeAsync(csentry);
                await provider.SetPasswordAsync(csentry, newPassword, options);
                this.logger.LogInformation($"Successfully set password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error setting password for {csentry.DN}");
                throw;
            }
        }

        public async Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            try
            {
                this.logger.LogInformation($"Changing password for: {csentry.DN}");
                IObjectPasswordProvider provider = await this.GetProviderForTypeAsync(csentry);
                await provider.ChangePasswordAsync(csentry, oldPassword, newPassword);
                this.logger.LogInformation($"Successfully changed password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Error changing password for {csentry.DN}");
                throw;
            }
        }

        private async Task<IObjectPasswordProvider> GetProviderForTypeAsync(CSEntry csentry)
        {
            foreach (IObjectPasswordProvider provider in this.Providers)
            {
                if (await provider.CanPerformPasswordOperationAsync(csentry))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException($"An export provider for the type '{csentry.ObjectType}' could not be found");
        }

        private async Task InitializeProvidersAsync(PasswordContext context)
        {
            foreach (var provider in this.Providers)
            {
                await provider.InitializeAsync(context);
            }
        }
    }
}