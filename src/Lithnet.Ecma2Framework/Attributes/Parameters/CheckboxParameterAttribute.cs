using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CheckboxParameterAttribute : DataParameterAttribute
    {
        public CheckboxParameterAttribute(string name) : base(name)
        {
        }
    }
}
