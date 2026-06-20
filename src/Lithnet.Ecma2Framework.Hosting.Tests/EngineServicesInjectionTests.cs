using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Lithnet.Ecma2Framework.Internal;
using Lithnet.Ecma2Framework.Serialization;
using Lithnet.Ecma2Framework.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// Proves the Part B engine-services injection (Path C): the host's <c>Utils.ExtensionsDirectory</c>,
    /// captured engine-side by the shim and sent on the connection-open handshake, reaches the worker and is
    /// retrievable by providers through <see cref="IEngineServices"/>. The worker is net8, where the real
    /// <c>Utils.ExtensionsDirectory</c> would resolve to the WORKER's own directory; the injected value
    /// overrides that.
    ///
    /// The tests use a minimal inline consumer (via <see cref="WorkerHost.Create"/>) so they exercise the real
    /// DI registration and the real <see cref="SchemaRpcTarget"/> open handshake without depending on a
    /// rewired consumer assembly.
    /// </summary>
    [TestClass]
    public class EngineServicesInjectionTests
    {
        // A no-op startup that registers a single schema provider so the import orchestrator can open.
        private sealed class MinimalStartup : IEcmaStartup
        {
            public void Configure(IConfigurationBuilder builder)
            {
            }

            public void SetupServices(IServiceCollection services, IConfigParameters configParameters)
            {
                services.AddSingleton<ISchemaProvider, EmptySchemaProvider>();
                services.AddSingleton<IObjectImportProvider, NoOpImportProvider>();
            }
        }

        private sealed class EmptySchemaProvider : ISchemaProvider
        {
            public Task<Schema> GetMmsSchemaAsync()
            {
                return Task.FromResult(Schema.Create());
            }
        }

        private sealed class NoOpImportProvider : IObjectImportProvider
        {
            public Task InitializeAsync(ImportContext context)
            {
                return Task.CompletedTask;
            }

            public Task<bool> CanImportAsync(SchemaType type)
            {
                return Task.FromResult(false);
            }

            public Task GetCSEntryChangesAsync(
                SchemaType type,
                ICSEntryChangeCollection csentryCollection,
                string incomingWatermark,
                System.Threading.CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task<string> GetOutboundWatermark(SchemaType type, System.Threading.CancellationToken cancellationToken)
            {
                return Task.FromResult<string>(null);
            }
        }

        private static WorkerHost BuildHost()
        {
            WorkerHost host = WorkerHost.Create(new MinimalStartup(), new DefaultConfigRegistrationProvider());
            host.BuildContainer(new ConfigParameterKeyedCollection());
            return host;
        }

        private static string BuildSchemaXml()
        {
            return MmsPipeSerializer.SerializeXml<Schema>(Schema.Create());
        }

        private static string BuildRunStepXml()
        {
            OpenImportConnectionRunStep runStep =
                new OpenImportConnectionRunStep(null, OperationType.Full, 100, null, null, null);
            return MmsPipeSerializer.SerializeXml<OpenImportConnectionRunStep>(runStep);
        }

        [TestMethod]
        public void IEngineServicesIsRegisteredAsASingleton()
        {
            WorkerHost host = BuildHost();

            IEngineServices viaInterface = host.Services.GetService<IEngineServices>();
            EngineServices viaConcrete = host.Services.GetService<EngineServices>();

            Assert.IsNotNull(viaInterface, "IEngineServices must be resolvable from the worker container.");
            Assert.IsNotNull(viaConcrete, "EngineServices must be resolvable from the worker container.");
            Assert.AreSame(viaInterface, viaConcrete, "The interface and concrete registrations must be the same instance.");
        }

        [TestMethod]
        public async Task OpenImportInjectsExtensionsDirectoryRetrievableViaEngineServices()
        {
            const string hostExtensionsDir = @"C:\Program Files\Microsoft Forefront Identity Manager\2010\Synchronization Service\Extensions";

            WorkerHost host = BuildHost();
            SchemaRpcTarget target = new SchemaRpcTarget(host);

            // Drive the real OpenImport handshake carrying the injected ExtensionsDirectory.
            await target.OpenImport(BuildRunStepXml(), BuildSchemaXml(), null, hostExtensionsDir);

            IEngineServices engineServices = host.Services.GetService<IEngineServices>();
            Assert.AreEqual(
                hostExtensionsDir,
                engineServices.ExtensionsDirectory,
                "The worker must expose the injected ExtensionsDirectory through IEngineServices.");

            await target.CloseImport(null);
        }

        [TestMethod]
        public async Task OpenImportWithNullExtensionsDirectoryLeavesAccessorNull()
        {
            WorkerHost host = BuildHost();
            SchemaRpcTarget target = new SchemaRpcTarget(host);

            await target.OpenImport(BuildRunStepXml(), BuildSchemaXml(), null, null);

            IEngineServices engineServices = host.Services.GetService<IEngineServices>();
            Assert.IsNull(
                engineServices.ExtensionsDirectory,
                "A null injected value must leave the accessor null (the value is genuinely optional).");

            await target.CloseImport(null);
        }
    }
}
