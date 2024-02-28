using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a multi-line text box in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class MultilineTextboxParameterAttribute : DataParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the MultilineTextboxParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        public MultilineTextboxParameterAttribute(string name) : base(name)
        {
        }
    }
}
