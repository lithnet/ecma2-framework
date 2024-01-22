using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class ConnectivityConfigurationAttribute : Attribute
    {
        public ConnectivityConfigurationAttribute(string name = "Ecma:Connectivity")
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
