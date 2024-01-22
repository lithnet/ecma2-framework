using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SchemaConfigurationAttribute : Attribute
    {
        public SchemaConfigurationAttribute(string name = "Ecma:Schema")
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
