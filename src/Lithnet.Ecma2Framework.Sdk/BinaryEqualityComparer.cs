using System.Collections.Generic;
using System.Linq;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Provides equality comparison for byte arrays based on their contents
    /// </summary>
    internal class BinaryEqualityComparer : IEqualityComparer<byte[]>
    {
        private static readonly BinaryEqualityComparer defaultComparer = new BinaryEqualityComparer();

        /// <summary>
        /// Gets the default singleton instance of the comparer
        /// </summary>
        public static BinaryEqualityComparer Default => defaultComparer;

        /// <summary>
        /// Determines whether two byte arrays are equal by comparing their contents
        /// </summary>
        /// <param name="x">The first byte array to compare</param>
        /// <param name="y">The second byte array to compare</param>
        /// <returns>True if the byte arrays contain the same sequence of bytes</returns>
        public bool Equals(byte[] x, byte[] y)
        {
            if (object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (x == null || y == null)
            {
                return false;
            }

            return x.SequenceEqual(y);
        }

        /// <summary>
        /// Returns a hash code for the specified byte array based on its contents
        /// </summary>
        /// <param name="obj">The byte array for which to compute a hash code</param>
        /// <returns>A content-based hash code for the byte array</returns>
        public int GetHashCode(byte[] obj)
        {
            if (obj == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 0;

                foreach (byte item in obj)
                {
                    hash = (31 * hash) + item.GetHashCode();
                }

                return hash;
            }
        }
    }
}
