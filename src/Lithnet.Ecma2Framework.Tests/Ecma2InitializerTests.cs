using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Lithnet.Ecma2Framework.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Tests
{
    [TestClass]
    public class Ecma2InitializerTests
    {
        /// <summary>
        /// Verifies that <see cref="Ecma2Initializer.Build"/> boots the DI host and correctly resolves
        /// all providers registered by the startup class, logging infrastructure, and the config-parameters
        /// wrapper — all resolved through the mirror object model with no dependency on MIM assemblies.
        /// </summary>
        [TestMethod]
        public void Build_ResolvesRegisteredProviders()
        {
            ConfigParameterKeyedCollection configParameters = BuildConfigParameters(
                ("LogLevel", "Info"),
                ("BatchSize", "100"));

            Ecma2Initializer initializer = new Ecma2Initializer(
                new TestStartup(),
                new TestConfigRegistrationProvider());

            IServiceProvider serviceProvider = initializer.Build(configParameters);

            // ISchemaProvider must resolve to the registered TestSchemaProvider.
            ISchemaProvider schemaProvider = serviceProvider.GetService<ISchemaProvider>();
            Assert.IsNotNull(schemaProvider, "ISchemaProvider must be resolvable from the container.");
            Assert.IsInstanceOfType(schemaProvider, typeof(TestSchemaProvider), "ISchemaProvider must be the registered TestSchemaProvider.");

            // IObjectImportProvider must resolve and include a TestImportProvider instance.
            IEnumerable<IObjectImportProvider> importProviders = serviceProvider.GetServices<IObjectImportProvider>();
            Assert.IsNotNull(importProviders, "IObjectImportProvider registrations must be present.");
            IObjectImportProvider[] providerArray = importProviders.ToArray();
            Assert.IsTrue(providerArray.Length > 0, "At least one IObjectImportProvider must be registered.");
            bool hasTestProvider = false;
            foreach (IObjectImportProvider provider in providerArray)
            {
                if (provider is TestImportProvider)
                {
                    hasTestProvider = true;
                    break;
                }
            }

            Assert.IsTrue(hasTestProvider, "IObjectImportProvider registrations must include a TestImportProvider.");

            // ILoggerFactory must be present (registered by AddLogging()) and functional.
            ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            Assert.IsNotNull(loggerFactory, "ILoggerFactory must be resolvable — AddLogging() must have been called.");
            ILogger logger = loggerFactory.CreateLogger("TestCategory");
            Assert.IsNotNull(logger, "ILoggerFactory.CreateLogger must return a non-null ILogger.");

            // IConfigParameters must be present and return the values that were passed in at Build time.
            IConfigParameters configParams = serviceProvider.GetService<IConfigParameters>();
            Assert.IsNotNull(configParams, "IConfigParameters must be resolvable from the container.");
            Assert.AreEqual("Info", configParams.GetString("LogLevel"), "GetString(\"LogLevel\") must return the value supplied in the KeyedCollection.");
            Assert.AreEqual("100", configParams.GetString("BatchSize"), "GetString(\"BatchSize\") must return the value supplied in the KeyedCollection.");
        }

        /// <summary>
        /// Verifies that a second call to <see cref="Ecma2Initializer.Build"/> returns the same
        /// <see cref="IServiceProvider"/> instance as the first call (the initializer caches the provider
        /// and only rebuilds the parameter reference on subsequent calls).
        /// </summary>
        [TestMethod]
        public void Build_SecondCall_ReturnsCachedServiceProvider()
        {
            ConfigParameterKeyedCollection firstParams = BuildConfigParameters(("LogLevel", "Info"));

            Ecma2Initializer initializer = new Ecma2Initializer(
                new TestStartup(),
                new TestConfigRegistrationProvider());

            IServiceProvider firstProvider = initializer.Build(firstParams);

            ConfigParameterKeyedCollection secondParams = BuildConfigParameters(("LogLevel", "Debug"));

            IServiceProvider secondProvider = initializer.Build(secondParams);

            // The initializer returns the cached provider; both calls must yield the same reference.
            Assert.AreSame(firstProvider, secondProvider, "Build must return the cached IServiceProvider on subsequent calls.");

            // The ConfigParameters wrapper must reflect the updated parameter collection.
            IConfigParameters configParams = secondProvider.GetService<IConfigParameters>();
            Assert.IsNotNull(configParams, "IConfigParameters must remain resolvable after a second Build call.");
            Assert.AreEqual("Debug", configParams.GetString("LogLevel"), "The parameter collection must be updated to the second set of parameters.");
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static ConfigParameterKeyedCollection BuildConfigParameters(params (string Name, string Value)[] entries)
        {
            ConfigParameterKeyedCollection collection = new ConfigParameterKeyedCollection();
            foreach ((string name, string value) in entries)
            {
                collection.Add(new ConfigParameter(name, value));
            }

            return collection;
        }
    }
}
