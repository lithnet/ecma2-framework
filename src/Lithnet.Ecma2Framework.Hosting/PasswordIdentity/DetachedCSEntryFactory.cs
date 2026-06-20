using System;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// Builds a read-only host <see cref="CSEntry"/> from the password-path identity carried across the pipe,
    /// so a password provider receives the real MMS <see cref="CSEntry"/> contract rather than a bespoke type.
    /// </summary>
    internal static class DetachedCSEntryFactory
    {
        /// <summary>
        /// Creates a <see cref="CSEntry"/> backed by the supplied identity.
        /// </summary>
        /// <param name="identity">The identity carried from the shim. Must not be null.</param>
        /// <returns>A read-only <see cref="CSEntry"/> exposing the identity surface.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
        public static CSEntry Create(CSEntryIdentity identity)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            return new DetachedCSEntry(identity);
        }
    }
}
