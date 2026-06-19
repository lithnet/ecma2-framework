using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a file selector in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FileParameterAttribute : DataParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the FileParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        public FileParameterAttribute(string name) : base(name)
        {
        }
    }
}
