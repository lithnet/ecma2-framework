﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// <para>Provides the main password management functionality for the ECMA2 framework</para>
    /// <para>This class is called by generated code, and should not be called directly</para>
    /// </summary>
    public class Ecma2Password : Ecma2Base
    {
        private List<IObjectPasswordProvider> providerCache;
        private PasswordContext context;

        public Ecma2Password(Ecma2Initializer initializer) : base(initializer)
        {
        }

        private List<IObjectPasswordProvider> Providers
        {
            get
            {
                this.providerCache ??= this.ServiceProvider.GetServices<IObjectPasswordProvider>().ToList();
                return this.providerCache;
            }
        }

        public Task<ConnectionSecurityLevel> GetConnectionSecurityLevelAsync()
        {
            return Task.FromResult(ConnectionSecurityLevel.Secure);
        }

        public async Task OpenPasswordConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            this.InitializeDIContainer(configParameters);

            this.context = new PasswordContext(partition);

            var initializer = this.ServiceProvider.GetService<IContextInitializer>();

            if (initializer != null)
            {
                this.Logger.LogInformation("Launching initializer");

                try
                {
                    await initializer.InitializePasswordOperationAsync(this.context);
                }
                catch (NotImplementedException) { }

                this.Logger.LogInformation("Initializer complete");
            }

            await this.InitializeProvidersAsync(this.context);
        }

        public Task ClosePasswordConnectionAsync()
        {
            return Task.CompletedTask;
        }

        public async Task SetPasswordAsync(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            try
            {
                this.Logger.LogTrace($"Setting password for: {csentry.DN}");
                IObjectPasswordProvider provider = await this.GetProviderForTypeAsync(csentry);
                await provider.SetPasswordAsync(csentry, newPassword, options);
                this.Logger.LogInformation($"Successfully set password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, $"Error setting password for {csentry.DN}");
                throw;
            }
        }

        public async Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            try
            {
                this.Logger.LogInformation($"Changing password for: {csentry.DN}");
                IObjectPasswordProvider provider = await this.GetProviderForTypeAsync(csentry);
                await provider.ChangePasswordAsync(csentry, oldPassword, newPassword);
                this.Logger.LogInformation($"Successfully changed password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                this.Logger.LogError(ex, $"Error changing password for {csentry.DN}");
                throw;
            }
        }

        private async Task<IObjectPasswordProvider> GetProviderForTypeAsync(CSEntry csentry)
        {
            foreach (IObjectPasswordProvider provider in this.Providers)
            {
                try
                {
                    if (await provider.CanPerformPasswordOperationAsync(csentry))
                    {
                        return provider;
                    }
                }
                catch (NotImplementedException) { }
            }

            throw new InvalidOperationException($"An export provider for the type '{csentry.ObjectType}' could not be found");
        }

        private async Task InitializeProvidersAsync(PasswordContext context)
        {
            foreach (var provider in this.Providers)
            {
                try
                {
                    await provider.InitializeAsync(context);
                }
                catch (NotImplementedException) { }
            }
        }
    }
}