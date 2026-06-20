using System.Collections.Generic;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.Hosting;
using Lithnet.Ecma2Framework.TestConsumer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// Unit tests that drive <see cref="Ecma2InitOrchestrator"/> directly using the real
    /// <c>TestCapabilitiesProvider</c>, <c>TestSchemaProvider</c>, and
    /// <c>TestConfigParametersProvider</c> from the TestConsumer assembly loaded via
    /// <see cref="WorkerHost"/>.
    ///
    /// These tests verify the orchestrator contract without going through the RPC pipe.
    /// </summary>
    /// <remarks>
    /// Expected data from the TestConsumer:
    /// <list type="bullet">
    ///   <item><c>TestSchemaProvider</c> returns a "user" type with "id" anchor and "displayName" attribute.</item>
    ///   <item><c>TestCapabilitiesProvider</c> returns SupportImport=true, SupportExport=true, SupportPassword=true.</item>
    ///   <item><c>TestConfigParametersProvider</c> returns empty definitions for all pages.</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A missing TestConsumer DLL causes the test to fail with a clear assertion message.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class InitOrchestratorTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static WorkerHost LoadHost()
        {
            WorkerHost host = WorkerHost.Create(new TestConsumerStartup(), new DefaultConfigRegistrationProvider());
            host.BuildContainer(new ConfigParameterKeyedCollection());
            return host;
        }

        // -------------------------------------------------------------------------
        // Schema tests
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task GetSchemaAsync_ReturnsUserType()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);

            Schema schema = await orchestrator.GetSchemaAsync();

            Assert.IsNotNull(schema, "Schema must not be null");
            Assert.AreEqual(1, schema.Types.Count, "Schema must contain exactly one type");
            Assert.IsTrue(schema.Types.Contains("user"), "Schema must contain type 'user'");
        }

        [TestMethod]
        public async Task GetSchemaAsync_UserType_HasIdAnchor()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);

            Schema schema = await orchestrator.GetSchemaAsync();

            SchemaType userType = schema.Types["user"];
            Assert.IsTrue(userType.Attributes.Contains("id"), "User type must have 'id' attribute");
            Assert.IsTrue(userType.Attributes["id"].IsAnchor, "Attribute 'id' must be an anchor");
            Assert.AreEqual(AttributeType.String, userType.Attributes["id"].DataType);
        }

        [TestMethod]
        public async Task GetSchemaAsync_UserType_HasDisplayName()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);

            Schema schema = await orchestrator.GetSchemaAsync();

            SchemaType userType = schema.Types["user"];
            Assert.IsTrue(userType.Attributes.Contains("displayName"), "User type must have 'displayName' attribute");
            Assert.IsFalse(userType.Attributes["displayName"].IsAnchor, "Attribute 'displayName' must not be an anchor");
            Assert.AreEqual(AttributeType.String, userType.Attributes["displayName"].DataType);
        }

        // -------------------------------------------------------------------------
        // Capabilities tests
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task GetCapabilitiesAsync_ReturnsSupportImport()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            MACapabilities caps = await orchestrator.GetCapabilitiesAsync(configParameters);

            Assert.IsNotNull(caps, "Capabilities must not be null");
            Assert.IsTrue(caps.SupportImport, "TestCapabilitiesProvider sets SupportImport=true");
        }

        [TestMethod]
        public async Task GetCapabilitiesAsync_ReturnsSupportExport()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            MACapabilities caps = await orchestrator.GetCapabilitiesAsync(configParameters);

            Assert.IsTrue(caps.SupportExport, "TestCapabilitiesProvider sets SupportExport=true");
        }

        [TestMethod]
        public async Task GetCapabilitiesAsync_ReturnsSupportPassword()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            MACapabilities caps = await orchestrator.GetCapabilitiesAsync(configParameters);

            Assert.IsTrue(caps.SupportPassword, "TestCapabilitiesProvider sets SupportPassword=true");
        }

        // -------------------------------------------------------------------------
        // Config parameters tests
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task GetConfigParametersAsync_ConnectivityPage_ReturnsEmptyList()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            IList<ConfigParameterDefinition> defs = await orchestrator.GetConfigParametersAsync(
                configParameters,
                ConfigParameterPage.Connectivity,
                0);

            Assert.IsNotNull(defs, "Definitions list must not be null");
            Assert.AreEqual(0, defs.Count, "TestConfigParametersProvider returns empty list for Connectivity page");
        }

        [TestMethod]
        public async Task GetConfigParametersAsync_GlobalPage_ReturnsEmptyList()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            IList<ConfigParameterDefinition> defs = await orchestrator.GetConfigParametersAsync(
                configParameters,
                ConfigParameterPage.Global,
                0);

            Assert.AreEqual(0, defs.Count, "TestConfigParametersProvider returns empty list for Global page");
        }

        [TestMethod]
        public async Task ValidateConfigParametersAsync_ConnectivityPage_ReturnsSuccess()
        {
            WorkerHost host = LoadHost();
            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            ParameterValidationResult result = await orchestrator.ValidateConfigParametersAsync(
                configParameters,
                ConfigParameterPage.Connectivity,
                0);

            Assert.IsNotNull(result, "Validation result must not be null");
            Assert.AreEqual(ParameterValidationResultCode.Success, result.Code, "TestConfigParametersProvider validation returns Success");
        }
    }
}
