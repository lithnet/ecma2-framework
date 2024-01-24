using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a check box in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CheckboxParameterAttribute : DataParameterAttribute
    {
        public CheckboxParameterAttribute(string name) : base(name)
        {
        }
    }
}
