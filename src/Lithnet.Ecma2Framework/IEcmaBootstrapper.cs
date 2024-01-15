using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework
{
    public interface IEcmaBootstrapper
    {
        string EventLogName { get; }

        string EventLogSource { get; }

        string ManagementAgentName { get; }

        void SetupServices(IServiceCollection services);
    }
}
