using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a text box in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class StringParameterAttribute : DataParameterAttribute
    {
        /// <summary>
        /// Creates a new instance of the StringParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        public StringParameterAttribute(string name) : base(name)
        {
        }
    }
}