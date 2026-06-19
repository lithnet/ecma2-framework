using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Contains extensions for objects not directly related to MetadirectoryServices components, such as strings and DateTimes
    /// </summary>
    public static class GenericExtensions
    {
        /// <summary>
        /// A .NET custom format string that represents the ISO8601 date format that is used by the FIM Service
        /// </summary>
        public const string ResourceManagementServiceDateFormat = @"yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff";

        /// <summary>
        /// Gets an informative string representation of an object
        /// </summary>
        /// <param name="obj">The object to convert</param>
        /// <returns>An informative string representation of an object</returns>
        public static string ToSmartString(this object obj)
        {
            if (obj is byte[])
            {
                byte[] cast = (byte[])obj;
                return Convert.ToBase64String(cast);
            }
            else if (obj is long)
            {
                return ((long)obj).ToString();
            }
            else if (obj is string)
            {
                return ((string)obj).ToString();
            }
            else if (obj is bool)
            {
                return ((bool)obj).ToString();
            }
            else if (obj is Guid)
            {
                return ((Guid)obj).ToString();
            }
            else if (obj is DateTime)
            {
                return ((DateTime)obj).ToString(ResourceManagementServiceDateFormat);
            }
            else if (obj == null)
            {
                return "null";
            }
            else
            {
                return obj.ToString();
            }
        }
    }
}
