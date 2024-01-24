namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered in the UI as a control that stores data
    /// </summary>
    public abstract class DataParameterAttribute : ParameterAttribute
    {
        public DataParameterAttribute(string name) : base(name)
        {
        }
    }
}
