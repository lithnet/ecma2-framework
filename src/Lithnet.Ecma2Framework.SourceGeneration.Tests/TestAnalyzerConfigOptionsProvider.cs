using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    /// <summary>
    /// A test double for <see cref="AnalyzerConfigOptionsProvider"/> that exposes a fixed set of global build-property
    /// values via <see cref="GlobalOptions"/>. This is the mechanism the generator uses to read the
    /// Ecma2ManagementAgentName MSBuild property.
    /// </summary>
    internal class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly TestAnalyzerConfigOptions globalOptions;

        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> globalProperties)
        {
            this.globalOptions = new TestAnalyzerConfigOptions(globalProperties);
        }

        public override AnalyzerConfigOptions GlobalOptions => this.globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
        {
            return this.globalOptions;
        }

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
        {
            return this.globalOptions;
        }
    }
}
