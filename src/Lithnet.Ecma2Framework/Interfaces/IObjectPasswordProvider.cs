using System.Security;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectPasswordProvider
    {
        Task InitializeAsync(IPasswordContext context);

        Task<bool> CanPerformPasswordOperationAsync(CSEntry csentry);

        Task SetPasswordAsync(CSEntry csentry, SecureString newPassword, PasswordOptions options);

        Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword);
    }
}
