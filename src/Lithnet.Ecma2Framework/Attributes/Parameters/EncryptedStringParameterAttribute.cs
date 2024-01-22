using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class EncryptedStringParameterAttribute : DataParameterAttribute
    {
        public EncryptedStringParameterAttribute(string name) : base(name)
        {
        }
    }
}
