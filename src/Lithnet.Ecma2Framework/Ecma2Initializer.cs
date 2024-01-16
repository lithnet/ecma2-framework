using Microsoft.Extensions.DependencyInjection;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Initializer
    {
        public Ecma2Initializer(IEcmaBootstrapper bootstrapper)
        {
            this.Bootstrapper = bootstrapper;
        }

        private void AddInternalServices()
        {
        }

        public IServiceCollection Services { get; private set; }

        public IEcmaBootstrapper Bootstrapper { get; }

        internal ServiceProvider Build(IConfigParameters config)
        {
            this.Services = new ServiceCollection();
            this.Services.AddSingleton<IConfigParameters>(config);
            this.Bootstrapper.SetupServices(this.Services, config);
            this.AddInternalServices();
            return this.Services.BuildServiceProvider();
        }
    }
}
