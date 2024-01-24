using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a drop down control in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DropdownParameterAttribute : DataParameterAttribute
    {
        public DropdownParameterAttribute(string name, bool extensible = false, string[] displayedValues = null) : base(name)
        {
            this.Extensible = extensible;
            this.DisplayedValues = displayedValues;
        }

        public bool Extensible { get; }

        public string[] DisplayedValues { get; }
    }
}
