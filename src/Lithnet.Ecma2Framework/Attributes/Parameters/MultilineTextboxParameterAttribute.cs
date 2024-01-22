using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class MultilineTextboxParameterAttribute : DataParameterAttribute
    {
        public MultilineTextboxParameterAttribute(string name) : base(name)
        {
        }
    }
}
