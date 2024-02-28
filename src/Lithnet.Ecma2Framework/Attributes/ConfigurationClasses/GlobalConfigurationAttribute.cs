using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Global page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class GlobalConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the GlobalConfigurationAttribute class
        /// </summary>
        public GlobalConfigurationAttribute() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the GlobalConfigurationAttribute class
        /// </summary>
        /// <param name="name">An optional name of the configuration section. This value defaults to Ecma:Global</param>
        public GlobalConfigurationAttribute(string name)
        {
            this.Name = name ??  "Ecma:Global";
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
