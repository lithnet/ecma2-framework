using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class PartitionConfigurationAttribute : Attribute
    {
        public PartitionConfigurationAttribute(string name = "Ecma:Partition")
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
