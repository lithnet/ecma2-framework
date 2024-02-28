using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a drop down control in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DropdownParameterAttribute : DataParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the DropdownParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        /// <param name="extensible">A boolean value that indicates if users should be able to specify their own value in addition to the displayed values</param>
        /// <param name="displayedValues">The list of values that should be displayed in the drop down list</param>
        public DropdownParameterAttribute(string name, bool extensible = false, string[] displayedValues = null) : base(name)
        {
            this.Extensible = extensible;
            this.DisplayedValues = displayedValues;
        }

        /// <summary>
        /// Gets a boolean value that indicates if users should be able to specify their own value in addition to the displayed values
        /// </summary>
        public bool Extensible { get; }

        /// <summary>
        /// Gets the list of values that should be displayed in the drop down list
        /// </summary>
        public string[] DisplayedValues { get; }
    }
}
