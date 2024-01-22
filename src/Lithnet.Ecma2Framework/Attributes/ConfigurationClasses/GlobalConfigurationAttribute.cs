using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GlobalConfigurationAttribute : Attribute
    {
        public GlobalConfigurationAttribute(string name = "Ecma:Global")
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
