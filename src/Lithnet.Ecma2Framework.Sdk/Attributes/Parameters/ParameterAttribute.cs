using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// The base class that represents configuration parameter attributes
    /// </summary>
    public abstract class ParameterAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the ParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        protected ParameterAttribute(string name)
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name of the parameter, as shown to the user on the MIM configuration page
        /// </summary>
        public string Name { get; }
    }
}
