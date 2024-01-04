using System.Security;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectPasswordProviderAsync
    {
        void Initialize(IPasswordContext context);

        bool CanPerformPasswordOperation(CSEntry csentry);

        Task SetPasswordAsync(CSEntry csentry, SecureString newPassword, PasswordOptions options);

        Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword);
    }
}