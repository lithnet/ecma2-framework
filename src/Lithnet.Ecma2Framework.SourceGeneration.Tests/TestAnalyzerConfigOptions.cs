using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    /// <summary>
    /// A test double for <see cref="AnalyzerConfigOptions"/> that serves a fixed set of build-property values to the
    /// generator. The real implementation reads these from MSBuild; in the tests they are injected directly.
    /// </summary>
    internal class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> values;

        public TestAnalyzerConfigOptions(Dictionary<string, string> values)
        {
            this.values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            return this.values.TryGetValue(key, out value);
        }
    }
}
