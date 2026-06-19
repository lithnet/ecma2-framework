using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    /// <summary>
    /// Defines a test-instrumentation hook that the test password provider implements to record
    /// the metadata and secret hashes from the last password operation.
    /// </summary>
    /// <remarks>
    /// TEST INSTRUMENTATION ONLY — this interface exists solely so the password tests can verify
    /// that secrets crossed the named-pipe channel correctly without exposing plaintext over any
    /// channel. It lives in this test-support consumer assembly (alongside its implementer,
    /// <see cref="TestPasswordProviderImpl"/>) — NOT in the production framework — and is referenced
    /// by the test projects, which already reference this consumer.
    ///
    /// Security contract:
    /// <list type="bullet">
    ///   <item>Hash fields contain base-64 encoded SHA-256 digests of the UTF-8 bytes of the
    ///     plaintext password values.  Plaintext is NEVER stored here.</item>
    ///   <item>An empty string in a hash field indicates that no value was recorded for that
    ///     field (e.g. <see cref="LastOldPasswordSha256"/> after a SetPassword call).</item>
    /// </list>
    ///
    /// Production providers must NOT implement this interface.
    /// </remarks>
    public interface IPasswordCallRecorder
    {
        /// <summary>Gets the DN from the last password call. TEST INSTRUMENTATION ONLY.</summary>
        string LastDn { get; }

        /// <summary>Gets the ObjectType from the last password call. TEST INSTRUMENTATION ONLY.</summary>
        string LastObjectType { get; }

        /// <summary>
        /// Gets the string representation of the <see cref="PasswordOptions"/> from the last
        /// SetPassword call. Empty string after a ChangePassword call. TEST INSTRUMENTATION ONLY.
        /// </summary>
        string LastOptions { get; }

        /// <summary>
        /// Gets the base-64 encoded SHA-256 of the new password UTF-8 bytes from the last
        /// SetPassword or ChangePassword call. Empty string when no value was recorded.
        /// TEST INSTRUMENTATION ONLY.
        /// </summary>
        string LastNewPasswordSha256 { get; }

        /// <summary>
        /// Gets the base-64 encoded SHA-256 of the old password UTF-8 bytes from the last
        /// ChangePassword call. Empty string after a SetPassword call.
        /// TEST INSTRUMENTATION ONLY.
        /// </summary>
        string LastOldPasswordSha256 { get; }
    }
}
