using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a check box in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CheckboxParameterAttribute : DataParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the CheckboxParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        public CheckboxParameterAttribute(string name) : base(name)
        {
        }
    }
}
