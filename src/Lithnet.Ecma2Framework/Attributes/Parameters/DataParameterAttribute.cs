namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered in the UI as a control that stores data
    /// </summary>
    public abstract class DataParameterAttribute : ParameterAttribute
    {
        /// <summary>
        /// Initializes a new instance of the DataParameterAttribute class
        /// </summary>
        /// <param name="name">The name of the parameter, as shown to the user on the MIM configuration page</param>
        protected DataParameterAttribute(string name) : base(name)
        {
        }
    }
}
