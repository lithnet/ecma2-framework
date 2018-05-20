using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security;
using Microsoft.MetadirectoryServices;
using NLog;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Password :
        IMAExtensible2Password
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static List<IObjectPasswordProvider> providerCache;

        private static List<IObjectPasswordProvider> Providers
        {
            get
            {
                if (Ecma2Password.providerCache == null)
                {
                    Ecma2Password.providerCache = InterfaceManager.GetInstancesOfType<IObjectPasswordProvider>().ToList();
                }

                return Ecma2Password.providerCache;
            }
        }

        private PasswordContext passwordContext;

        public void OpenPasswordConnection(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            Logging.SetupLogger(configParameters);
            this.passwordContext = new PasswordContext()
            {
                ConnectionContext = InterfaceManager.GetProviderOrDefault<IConnectionContextProvider>()?.GetConnectionContext(configParameters, ConnectionContextOperationType.Password),
                ConfigParameters = configParameters
            };
        }

        public void ClosePasswordConnection()
        {
        }

        public ConnectionSecurityLevel GetConnectionSecurityLevel()
        {
            return ConnectionSecurityLevel.Secure;
        }

        public void SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            try
            {
                logger.Trace($"Setting password for: {csentry.DN}");
                IObjectPasswordProvider provider = this.GetProviderForType(csentry);
                provider.SetPassword(csentry, newPassword, options, this.passwordContext);
                logger.Info($"Successfully set password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error setting password for {csentry.DN}");
                logger.Error(ex.UnwrapIfSingleAggregateException());
                throw;
            }
        }

        public void ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            try
            {
                logger.Info($"Changing password for: {csentry.DN}");
                IObjectPasswordProvider provider = this.GetProviderForType(csentry);
                provider.ChangePassword(csentry, oldPassword, newPassword, this.passwordContext);
                logger.Info($"Successfully changed password for: {csentry.DN}");
            }
            catch (Exception ex)
            {
                logger.Error($"Error changing password for {csentry.DN}");
                logger.Error(ex.UnwrapIfSingleAggregateException());
                throw;
            }
        }

        private IObjectPasswordProvider GetProviderForType(CSEntry csentry)
        {
            foreach (IObjectPasswordProvider provider in Ecma2Password.Providers)
            {
                if (provider.CanPerformPasswordOperation(csentry))
                {
                    return provider;
                }
            }

            throw new InvalidOperationException($"An export provider for the type '{csentry.ObjectType}' could not be found");
        }
    }
}