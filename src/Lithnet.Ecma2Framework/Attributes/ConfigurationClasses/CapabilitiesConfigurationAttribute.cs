using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CapabilitiesConfigurationAttribute : Attribute
    {
        public CapabilitiesConfigurationAttribute(string name = "Ecma:Capabilities")
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
