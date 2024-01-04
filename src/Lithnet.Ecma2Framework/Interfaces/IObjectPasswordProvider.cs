using System.Security;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectPasswordProvider
    {
        void Initialize(IPasswordContext context);

        bool CanPerformPasswordOperation(CSEntry csentry);

        void SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options);

        void ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword);
    }
}
