using System;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// A class that contains the configuration options for the Ecma2Framework
    /// </summary>
    public class Ecma2FrameworkOptions
    {
        /// <summary>
        /// Gets or sets the number of threads to use for import operations. The default value is the number of logical processors on the machine multiplied by 2
        /// </summary>
        public int ExportThreads { get; set; } = Environment.ProcessorCount * 2;
    }
}
