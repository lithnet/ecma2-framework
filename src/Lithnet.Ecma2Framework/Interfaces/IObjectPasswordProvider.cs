using System.Security;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines the methods and properties that a password provider must implement
    /// </summary>
    public interface IObjectPasswordProvider
    {
        /// <summary>
        /// Initializes the password provider. This method is called once at the start of a batch of password operations
        /// </summary>
        /// <param name="context">The context of the operation</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task InitializeAsync(PasswordContext context);

        /// <summary>
        /// Indicates whether the password provider can perform password operations on the specified object
        /// </summary>
        /// <param name="csentry">The CSEntry representing the object with the associated password change</param>
        /// <returns><see langword="true"/> if the provider can process the password change, otherwise <see langword="false"/> </returns>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task<bool> CanPerformPasswordOperationAsync(CSEntry csentry);

        /// <summary>
        /// Sets the password on the specified object
        /// </summary>
        /// <param name="csentry">The CSEntry representing the object whose password should be set</param>
        /// <param name="newPassword">The new password</param>
        /// <param name="options">Flags that indicate additional steps that may need to be taken once the password it set</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task SetPasswordAsync(CSEntry csentry, SecureString newPassword, PasswordOptions options);

        /// <summary>
        /// Changes the password on the specified object
        /// </summary>
        /// <param name="csentry">The CSEntry representing the object whose password should be changed</param>
        /// <param name="oldPassword">The old password</param>
        /// <param name="newPassword">The new password</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword);
    }
}
