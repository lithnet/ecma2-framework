using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a heading label in the management agent's configuration pages
    /// The label has no data backing and therefore is not associated with a property, rather it is added to an existing property. The label will be displayed above the decorated property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class LabelParameterAttribute : UIParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the LabelParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        public LabelParameterAttribute(string name) : base(name)
        {
        }
    }
}
