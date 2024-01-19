using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class LabelParameterAttribute : UIParameterAttribute
    {
        public LabelParameterAttribute(string name) : base(name)
        {
        }
    }
}
