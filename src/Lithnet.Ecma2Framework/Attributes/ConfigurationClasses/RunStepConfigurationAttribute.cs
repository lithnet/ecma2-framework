using System;

namespace Lithnet.Ecma2Framework
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RunStepConfigurationAttribute : Attribute
    {
        public RunStepConfigurationAttribute(string name = "Ecma:RunStep")
        {
            this.Name = name;
        }

        public string Name { get; }
    }
}
