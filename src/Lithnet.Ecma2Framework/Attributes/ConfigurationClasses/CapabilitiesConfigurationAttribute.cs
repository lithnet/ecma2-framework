﻿using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Capabilities page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class CapabilitiesConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the CapabilitiesConfigurationAttribute class
        /// </summary>
        public CapabilitiesConfigurationAttribute() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the CapabilitiesConfigurationAttribute class
        /// </summary>
        /// <param name="name">An optional name of the configuration section. This value defaults to Ecma:Capabilities</param>
        public CapabilitiesConfigurationAttribute(string name)
        {
            this.Name = name ?? "Ecma:Capabilities";
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
