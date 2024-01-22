using System;
using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public abstract class Ecma2Base
    {
        private readonly Ecma2Initializer initializer;

        protected ILogger Logger { get; private set; }

        protected IConfigParameters ConfigParameters { get; private set; }

        protected IServiceProvider ServiceProvider { get; private set; }

        protected Ecma2Base(Ecma2Initializer initializer)
        {
            this.initializer = initializer;
        }

        protected void InitializeDIContainer(KeyedCollection<string, ConfigParameter> configParameters)
        {
            this.ServiceProvider = this.initializer.Build(configParameters);
            this.Logger = this.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(this.GetType().FullName);
            this.ConfigParameters = this.ServiceProvider.GetRequiredService<IConfigParameters>();
        }
    }
}