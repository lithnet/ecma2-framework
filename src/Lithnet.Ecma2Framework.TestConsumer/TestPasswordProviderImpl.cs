using System;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    /// <summary>
    /// Test implementation of <see cref="IObjectPasswordProvider"/> that records calls for
    /// instrumentation and implements <see cref="IPasswordCallRecorder"/> so the worker's
    /// test-instrumentation RPC can expose hashes of the last password operation.
    /// </summary>
    /// <remarks>
    /// TEST INSTRUMENTATION ONLY — not for production use.
    ///
    /// Security:
    /// <list type="bullet">
    ///   <item>Plaintext is recovered from the <see cref="SecureString"/> solely to compute a
    ///     SHA-256 hash for test assertion purposes.  The plaintext string is not stored in any
    ///     field.  Only the base-64 encoded SHA-256 digest is retained.</item>
    ///   <item>No password value is logged or written to any output stream.</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>If the entry DN contains the string <c>"failme"</c>, the password operation
    ///     throws <see cref="InvalidOperationException"/> with a generic failure message.
    ///     The exception message does NOT contain any secret value.</item>
    /// </list>
    /// </remarks>
    public sealed class TestPasswordProviderImpl : IObjectPasswordProvider, IPasswordCallRecorder
    {
        private const string FailmeTrigger = "failme";
        private const string FailureMessage = "sample password failure";

        // -------------------------------------------------------------------------
        // IPasswordCallRecorder — hash fields only; plaintext is never stored
        // -------------------------------------------------------------------------

        /// <inheritdoc />
        public string LastDn { get; private set; }

        /// <inheritdoc />
        public string LastObjectType { get; private set; }

        /// <inheritdoc />
        public string LastOptions { get; private set; }

        /// <inheritdoc />
        public string LastNewPasswordSha256 { get; private set; }

        /// <inheritdoc />
        public string LastOldPasswordSha256 { get; private set; }

        // -------------------------------------------------------------------------
        // IObjectPasswordProvider
        // -------------------------------------------------------------------------

        /// <inheritdoc />
        public Task InitializeAsync(PasswordContext context)
        {
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<bool> CanPerformPasswordOperationAsync(CSEntry csentry)
        {
            return Task.FromResult(true);
        }

        /// <inheritdoc />
        public Task SetPasswordAsync(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            if (csentry == null)
            {
                throw new ArgumentNullException("csentry");
            }

            if (newPassword == null)
            {
                throw new ArgumentNullException("newPassword");
            }

            string dn = csentry.DN == null ? null : csentry.DN.ToString();

            if (dn != null && dn.IndexOf(FailmeTrigger, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            this.LastDn = dn;
            this.LastObjectType = csentry.ObjectType;
            this.LastOptions = options.ToString();

            // TEST INSTRUMENTATION ONLY: compute SHA-256 of the secret to allow test assertions
            // without storing or transmitting plaintext. Plaintext is a local variable here only.
            string plain = new System.Net.NetworkCredential(string.Empty, newPassword).Password;
            this.LastNewPasswordSha256 = ComputeSha256Base64(plain);
            this.LastOldPasswordSha256 = string.Empty;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task ChangePasswordAsync(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            if (csentry == null)
            {
                throw new ArgumentNullException("csentry");
            }

            if (oldPassword == null)
            {
                throw new ArgumentNullException("oldPassword");
            }

            if (newPassword == null)
            {
                throw new ArgumentNullException("newPassword");
            }

            string dn = csentry.DN == null ? null : csentry.DN.ToString();

            if (dn != null && dn.IndexOf(FailmeTrigger, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                throw new InvalidOperationException(FailureMessage);
            }

            this.LastDn = dn;
            this.LastObjectType = csentry.ObjectType;
            this.LastOptions = string.Empty;

            // TEST INSTRUMENTATION ONLY: compute SHA-256 of each secret to allow test assertions
            // without storing or transmitting plaintext. Both plaintext values are locals only.
            string oldPlain = new System.Net.NetworkCredential(string.Empty, oldPassword).Password;
            string newPlain = new System.Net.NetworkCredential(string.Empty, newPassword).Password;
            this.LastOldPasswordSha256 = ComputeSha256Base64(oldPlain);
            this.LastNewPasswordSha256 = ComputeSha256Base64(newPlain);

            return Task.CompletedTask;
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Computes the base-64 encoded SHA-256 hash of the UTF-8 bytes of <paramref name="value"/>.
        /// Returns an empty string when <paramref name="value"/> is null or empty.
        /// </summary>
        private static string ComputeSha256Base64(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(utf8Bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}
