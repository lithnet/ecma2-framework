using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class StringParameterAttribute : DataParameterAttribute
    {
        public StringParameterAttribute(string name) : base(name)
        {
        }
    }
}