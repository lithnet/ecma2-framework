using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectPasswordProvider
    {
        bool CanPerformPasswordOperation(CSEntry csentry);

        void SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options, PasswordContext context);

        void ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword, PasswordContext context);
    }
} 
