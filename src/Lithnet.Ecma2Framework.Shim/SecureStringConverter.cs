using System;
using System.Runtime.InteropServices;
using System.Security;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Converts a <see cref="SecureString"/> to its plaintext representation at the
    /// wire boundary, zeroing the unmanaged buffer immediately after the copy.
    /// </summary>
    /// <remarks>
    /// Security contract:
    /// <list type="bullet">
    ///   <item>The plaintext buffer is allocated via <see cref="Marshal.SecureStringToGlobalAllocUnicode"/>
    ///     so the CLR does not touch managed heap with the secret.</item>
    ///   <item>The buffer is zeroed and freed via <see cref="Marshal.ZeroFreeGlobalAllocUnicode"/>
    ///     in a <c>finally</c> block so the plaintext is not retained in unmanaged memory
    ///     regardless of whether <see cref="Marshal.PtrToStringUni"/> throws.</item>
    ///   <item>The returned <see cref="string"/> is a managed copy and subject to normal GC
    ///     lifetime — callers must scope it as narrowly as possible, assign only to a local,
    ///     and avoid retaining it beyond the wire-send call.</item>
    ///   <item>This method MUST NOT be called from any logging or diagnostic code path.</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A null <paramref name="secureString"/> throws <see cref="ArgumentNullException"/>.</item>
    ///   <item>Any exception from <see cref="Marshal.PtrToStringUni"/> propagates after the
    ///     buffer is zeroed.</item>
    /// </list>
    /// </remarks>
    internal static class SecureStringConverter
    {
        /// <summary>
        /// Converts a <see cref="SecureString"/> to a plaintext <see cref="string"/>.
        /// The unmanaged buffer is zeroed and freed in a <c>finally</c> block regardless
        /// of success or failure.
        /// </summary>
        /// <param name="secureString">The secure string to convert.  Must not be null.</param>
        /// <returns>The plaintext content of <paramref name="secureString"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="secureString"/> is null.
        /// </exception>
        public static string ToPlainText(SecureString secureString)
        {
            if (secureString == null)
            {
                throw new ArgumentNullException("secureString");
            }

            IntPtr ptr = IntPtr.Zero;

            try
            {
                ptr = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return Marshal.PtrToStringUni(ptr);
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(ptr);
                }
            }
        }
    }
}
