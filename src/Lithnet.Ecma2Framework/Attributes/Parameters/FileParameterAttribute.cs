using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FileParameterAttribute : DataParameterAttribute
    {
        public FileParameterAttribute(string name) : base(name)
        {
        }
    }
}
