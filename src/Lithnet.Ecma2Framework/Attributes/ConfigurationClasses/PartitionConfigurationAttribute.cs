using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Partition page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class PartitionConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the PartitionConfigurationAttribute class
        /// </summary>
        public PartitionConfigurationAttribute() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PartitionConfigurationAttribute class
        /// </summary>
        /// <param name="name">An optional name of the configuration section. This value defaults to Ecma:Partition</param>
        public PartitionConfigurationAttribute(string name)
        {
            this.Name = name ?? "Ecma:Partition";
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
