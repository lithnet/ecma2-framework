using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a textbox in the management agent's configuration pages, and is stored as an encrypted string
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class EncryptedStringParameterAttribute : DataParameterAttribute
    {
        public EncryptedStringParameterAttribute(string name) : base(name)
        {
        }
    }
}
