using System.Collections.Generic;
using System.Linq;
using Lithnet.Ecma2Framework.Hosting;
using Lithnet.Ecma2Framework.TestConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    [TestClass]
    public sealed class WorkerHostTests
    {
        [TestMethod]
        public void WorkerHost_Create_BuildsContainer_ResolvesProviders()
        {
            // The generated host Main builds the WorkerHost from the consumer's startup directly (no
            // reflection); this mirrors that compile-time-bound path with the referenced TestConsumer startup.
            ConfigParameterKeyedCollection configParameters = new ConfigParameterKeyedCollection();

            WorkerHost host = WorkerHost.Create(new TestConsumerStartup(), new DefaultConfigRegistrationProvider());
            host.BuildContainer(configParameters);

            // Verify schema provider resolves and is the correct type.
            ISchemaProvider schemaProvider = host.GetSchemaProvider();
            Assert.IsNotNull(schemaProvider, "ISchemaProvider should not be null.");
            Assert.AreEqual(
                "Lithnet.Ecma2Framework.TestConsumer.TestSchemaProvider",
                schemaProvider.GetType().FullName,
                "Expected TestSchemaProvider to be registered.");

            // Verify import provider is registered.
            IEnumerable<IObjectImportProvider> importProviders = host.GetImportProviders();
            Assert.IsNotNull(importProviders, "IObjectImportProvider enumerable should not be null.");
            List<IObjectImportProvider> importList = importProviders.ToList();
            Assert.AreEqual(1, importList.Count, "Expected exactly one IObjectImportProvider.");
            Assert.AreEqual(
                "Lithnet.Ecma2Framework.TestConsumer.TestImportProvider",
                importList[0].GetType().FullName,
                "Expected TestImportProvider to be registered.");

            // Verify ILoggerFactory resolves (framework registers logging).
            ILoggerFactory loggerFactory = host.Services.GetService<ILoggerFactory>();
            Assert.IsNotNull(loggerFactory, "ILoggerFactory should be resolvable from the container.");
        }
    }
}
