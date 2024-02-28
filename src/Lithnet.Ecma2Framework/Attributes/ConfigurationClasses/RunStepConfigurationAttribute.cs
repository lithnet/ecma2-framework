using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An attribute that is used to decorate a class that contains configuration information that should be shown on the Run step page of the management agent configuration
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class RunStepConfigurationAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the RunStepConfigurationAttribute class
        /// </summary>
        public RunStepConfigurationAttribute() : this(null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the RunStepConfigurationAttribute class
        /// </summary>
        /// <param name="name">An optional name of the configuration section. This value defaults to Ecma:RunStep</param>
        public RunStepConfigurationAttribute(string name)
        {
            this.Name = name ?? "Ecma:RunStep";
        }

        /// <summary>
        /// Gets the name of the configuration section
        /// </summary>
        public string Name { get; }
    }
}
