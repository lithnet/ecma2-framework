using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework.SourceGenDebugger
{
    internal class Ecma2Initializer : IEcmaBootstrapper
    {
        public string EventLogName { get; } = "Application";

        public string EventLogSource { get; } = "Ecma2Framework";

        public string ManagementAgentName { get; } = "SourceGenDebugger";

        public void SetupServices(IServiceCollection services)
        {
        }
    }
}
