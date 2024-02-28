using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Connectivity page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConnectivityConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ConnectivityConfigurationAttribute class
        /// </summary>
        public ConnectivityConfigurationAttribute() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConnectivityConfigurationAttribute class
        /// </summary>
        /// <param name="name">An optional name of the configuration section. This value defaults to Ecma:Connectivity</param>
        public ConnectivityConfigurationAttribute(string name)
        {
            this.Name = name ?? "Ecma:Connectivity";
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
