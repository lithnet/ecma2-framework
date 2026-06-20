using System;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// Builds the <see cref="NotSupportedException"/> thrown by the password-path connector-space-entry
    /// types for members that are not meaningful on a password operation. On a password change the
    /// host <c>CSEntry</c> is an identity carrier only; the behavioural members (provisioning,
    /// deprovisioning, connection state, DN manipulation, mutation) are rules-extension semantics and
    /// have no off-engine meaning here, so they fail loud rather than return a misleading value.
    /// </summary>
    internal static class PasswordIdentityNotSupported
    {
        public static NotSupportedException Member(string member)
        {
            return new NotSupportedException(
                string.Format(
                    "'{0}' is not available on a connector-space entry presented to a password operation; " +
                    "the entry is an identity carrier only. This member is meaningful in a rules extension, not here.",
                    member));
        }
    }
}
