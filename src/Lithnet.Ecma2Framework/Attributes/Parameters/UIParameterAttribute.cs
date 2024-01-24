namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Base class that represents a configuration parameter that does not store data
    /// </summary>
    public abstract class UIParameterAttribute : ParameterAttribute
    {
        /// <summary>
        /// Creates a new instance of the UIParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the attribute</param>
        public UIParameterAttribute(string name) : base(name)
        {
        }
    }
}
