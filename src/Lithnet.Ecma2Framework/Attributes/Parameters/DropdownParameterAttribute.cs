using System;

namespace Lithnet.Ecma2Framework
{
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
