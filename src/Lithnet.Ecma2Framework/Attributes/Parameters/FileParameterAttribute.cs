using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a file selector in the management agent's configuration pages
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FileParameterAttribute : DataParameterAttribute
    {
        public FileParameterAttribute(string name) : base(name)
        {
        }
    }
}
