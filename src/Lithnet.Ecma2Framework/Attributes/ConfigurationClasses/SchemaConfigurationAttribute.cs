using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Schema page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SchemaConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the SchemaConfigurationAttribute class
        /// </summary>
        public SchemaConfigurationAttribute() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the SchemaConfigurationAttribute class
        /// </summary>
        /// <param name="name">An optional name of the configuration section. This value defaults to Ecma:Schema</param>
        public SchemaConfigurationAttribute(string name)
        {
            this.Name = name ?? "Ecma:Schema";
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
