using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines a configuration parameter that is rendered as a divider in the management agent's configuration pages
    /// The divider has no data backing and therefore is not associated with a property, rather it is added to an existing property. The divider will be displayed above the decorated property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class DividerParameterAttribute : Attribute
    {
    }
}
