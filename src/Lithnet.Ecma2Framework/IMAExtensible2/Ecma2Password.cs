using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Password
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static List<IObjectPasswordProvider> providerCache;

        private static List<IObjectPasswordProvider> Providers
        {
            get
            {
                Ecma2Password.providerCache ??= InterfaceManager.GetInstancesOfType<IObjectPasswordProvider>().ToList();
                return Ecma2Password.providerCache;
            }
        }

        private PasswordContext passwordContext;

        public Task<ConnectionSecurityLevel> GetConnectionSecurityLevelAsync()
        {
            return Task.FromResult(ConnectionSecurityLevel.Secure);
        }

        public async Task OpenPasswordConnectionAsync(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            Logging.SetupLogger(configParameters);
            this.passwordContext = new PasswordContext()
            {
                ConnectionContext = await InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContextAsync(configParameters, ConnectionContextOperationType.Password),
                ConfigParameters = configParameters
            };

            var initializers = InterfaceManager.GetInstancesOfType<IConnectionInitializer>();

            if (initializers != null)
            {
                foreach (var initializer in initializers)
                {
                    logger.Info("Launching initializer");
                    await initializer.InitializePasswordOperationAsync(this.passwordContext);
                    logger.Info("Initializer complete");
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
                logger.Trace($"Setting password for: {csentry.DN}");
                IObjectPasswordProvider provider = await this.GetProviderForTypeAsync(csentry);
                await provider.SetPasswordAsync(csentry, newPassword, options);
                logger.Info($"Successfully set password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting password for {csentry.DN}");
                logger.Error(ex);
                throw;
            }
        }

        public async Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            try
            {
                logger.Info($"Changing password for: {csentry.DN}");
                IObjectPasswordProvider provider = await this.GetProviderForTypeAsync(csentry);
                await provider.ChangePasswordAsync(csentry, oldPassword, newPassword);
                logger.Info($"Successfully changed password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error changing password for {csentry.DN}");
                logger.Error(ex);
                throw;
            }
        }

        private async Task<IObjectPasswordProvider> GetProviderForTypeAsync(CSEntry csentry)
        {
            foreach (IObjectPasswordProvider provider in Ecma2Password.Providers)
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
            foreach (var provider in Providers)
            {
                await provider.InitializeAsync(context);
            }
        }
    }
}