using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Initializer
    {
        public Ecma2Initializer(IEcmaBootstrapper bootstrapper)
        {
            this.Services = new ServiceCollection();
            this.Services.AddSingleton<IEcmaBootstrapper>(bootstrapper);
            bootstrapper.SetupServices(this.Services);
            this.AddInternalServices(bootstrapper);
        }

        private void AddInternalServices(IEcmaBootstrapper bootstrapper)
        {
            this.Services.AddSingleton<IEcma2ConfigParameters, Ecma2ConfigParameters>();
            this.Services.AddLogging(config =>
            {
                config.SetMinimumLevel(LogLevel.Trace);
                config.AddDebug();
                config.AddEventLog(settings =>
                {
                    settings.Filter = (source, level) => level >= LogLevel.Trace;
                    settings.LogName = bootstrapper.EventLogName ?? "Application";
                    settings.SourceName = bootstrapper.EventLogSource ?? bootstrapper.ManagementAgentName ?? "Lithnet.Ecma2Framework";
                });
            });
        }

        public IServiceCollection Services { get; }

        public ServiceProvider Build()
        {
            return this.Services.BuildServiceProvider();
        }
    }
}
