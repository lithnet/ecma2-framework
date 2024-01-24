using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// The base class that represents configuration parameter attributes
    /// </summary>
    public abstract class ParameterAttribute : Attribute
    {
        public ParameterAttribute(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of the configuration parameter
        /// </summary>
        public string Name { get; }
    }
}
