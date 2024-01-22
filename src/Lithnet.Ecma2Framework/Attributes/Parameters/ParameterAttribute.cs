using System;

namespace Lithnet.Ecma2Framework
{
    public abstract class ParameterAttribute : Attribute
    {
        public ParameterAttribute(string name)
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
