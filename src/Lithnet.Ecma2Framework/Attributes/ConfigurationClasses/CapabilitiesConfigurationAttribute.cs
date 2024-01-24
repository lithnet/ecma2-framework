using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Capabilities page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CapabilitiesConfigurationAttribute : Attribute
    {
        public CapabilitiesConfigurationAttribute(string name = "Ecma:Capabilities")
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
