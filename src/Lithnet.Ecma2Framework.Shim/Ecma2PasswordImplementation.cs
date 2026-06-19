using System.Collections.ObjectModel;
using System.Security;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The public host-facing password implementation. FIM instantiates this type to service password
    /// set/change calls. Each interface member forwards to an internal <see cref="PasswordConnection"/>,
    /// which marshals the call to the out-of-process worker over the named pipe.
    /// </summary>
    /// <remarks>
    /// Plain shared source compiled into every per-MA shim (not generated); the fixed type name is safe
    /// because FIM resolves the extension type from the specific per-MA shim assembly. See
    /// <see cref="Ecma2Implementation"/> for the full rationale.
    /// </remarks>
    public sealed class Ecma2PasswordImplementation : IMAExtensible2Password
    {
        private readonly PasswordConnection connection;

        public Ecma2PasswordImplementation()
        {
            string workerPath = WorkerPathResolver.Resolve();
            this.connection = new PasswordConnection(workerPath);
        }

        void IMAExtensible2Password.OpenPasswordConnection(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            this.connection.OpenPasswordConnection(configParameters, partition);
        }

        void IMAExtensible2Password.ClosePasswordConnection()
        {
            this.connection.ClosePasswordConnection();
        }

        ConnectionSecurityLevel IMAExtensible2Password.GetConnectionSecurityLevel()
        {
            return this.connection.GetConnectionSecurityLevel();
        }

        void IMAExtensible2Password.SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            this.connection.SetPassword(csentry, newPassword, options);
        }

        void IMAExtensible2Password.ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            this.connection.ChangePassword(csentry, oldPassword, newPassword);
        }
    }
}
