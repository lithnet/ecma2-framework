using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a multi-line text box in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class MultilineTextboxParameterAttribute : DataParameterAttribute
    {
        public MultilineTextboxParameterAttribute(string name) : base(name)
        {
        }
    }
}
