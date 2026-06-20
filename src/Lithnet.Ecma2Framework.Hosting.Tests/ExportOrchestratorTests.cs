using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.Hosting;
using Lithnet.Ecma2Framework.TestConsumer;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// Unit tests that drive <see cref="Ecma2ExportOrchestrator"/> directly using the real
    /// <c>TestExportProvider</c> from the TestConsumer assembly loaded via <see cref="WorkerHost"/>.
    /// These tests verify the per-entry routing, error isolation, and result identity contracts
    /// without going through the RPC pipe.
    /// </summary>
    /// <remarks>
    /// The TestConsumer's <c>TestExportProvider</c> claims all entries (CanExportAsync returns true),
    /// returns <see cref="MAExportError.Success"/> for normal entries, and
    /// <see cref="MAExportError.ExportErrorConnectedDirectoryError"/> for entries whose
    /// <c>ObjectType == "failme"</c>.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A null <c>workerHost</c> or missing consumer DLL causes the test to fail with a
    ///     clear assertion message rather than a cryptic NullReferenceException.</item>
    ///   <item>Provider-level exceptions are caught per entry and converted to
    ///     <see cref="MAExportError.ExportErrorCustomContinueRun"/> results; such failures surface
    ///     as test failures only when the test explicitly asserts them.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class ExportOrchestratorTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Builds a <see cref="WorkerHost"/> from the referenced TestConsumer startup (the same
        /// compile-time-bound path the generated host Main uses) and builds its container.
        /// </summary>
        private static WorkerHost LoadHost()
        {
            WorkerHost host = WorkerHost.Create(new TestConsumerStartup(), new DefaultConfigRegistrationProvider());
            host.BuildContainer(new ConfigParameterKeyedCollection());
            return host;
        }

        /// <summary>
        /// Builds a minimal <see cref="ExportContext"/> with a "user" schema and a Full export run step.
        /// </summary>
        private static ExportContext BuildExportContext()
        {
            Schema schema = Schema.Create();

            SchemaType userType = SchemaType.Create("user", false);
            userType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));
            schema.Types.Add(userType);

            // Real MMS OpenExportConnectionRunStep has no static Create factory (that was a mirror-only
            // shape); construct via its parameterized ctor: (partition, batchSize, exportType, inclusion,
            // exclusion). Preserves the original intent: Full export, batch size 100, no hierarchy nodes.
            OpenExportConnectionRunStep runStep = new OpenExportConnectionRunStep(
                null,
                100,
                OperationType.Full,
                null,
                null);

            return ExportContext.Create(runStep, schema);
        }

        /// <summary>
        /// Builds a CSEntryChange for the given object type.
        /// The framework assigns a new Guid on Create(); capture <c>entry.Identifier</c>
        /// after this call to use as the expected identifier in result assertions.
        /// </summary>
        private static CSEntryChange BuildEntry(string objectType)
        {
            CSEntryChange entry = CSEntryChange.Create();
            entry.ObjectModificationType = ObjectModificationType.Add;
            entry.DN = string.Format("CN={0}", objectType);
            entry.ObjectType = objectType;
            entry.AnchorAttributes.Add(AnchorAttribute.Create("id", objectType));
            entry.AttributeChanges.Add(
                AttributeChange.CreateAttributeAdd("displayName", new List<object> { "Test User" }));
            return entry;
        }

        // -------------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------------

        /// <summary>
        /// PutAsync with a batch of three entries returns exactly three results.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_PutAsync_ReturnsOneResultPerEntry()
        {
            WorkerHost host = LoadHost();
            ExportContext context = BuildExportContext();
            Ecma2ExportOrchestrator orchestrator = new Ecma2ExportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            List<CSEntryChange> entries = new List<CSEntryChange>
            {
                BuildEntry("user"),
                BuildEntry("user"),
                BuildEntry("failme"),
            };

            IList<CSEntryChangeResult> results = await orchestrator.PutAsync(entries, CancellationToken.None);

            Assert.AreEqual(3, results.Count, "PutAsync must return exactly one result per input entry");

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// Result identifiers from PutAsync match the corresponding input entry identifiers.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_PutAsync_ResultIdentifiers_MatchInputs()
        {
            WorkerHost host = LoadHost();
            ExportContext context = BuildExportContext();
            Ecma2ExportOrchestrator orchestrator = new Ecma2ExportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            CSEntryChange entry1 = BuildEntry("user");
            CSEntryChange entry2 = BuildEntry("user");
            CSEntryChange entryFail = BuildEntry("failme");

            Guid id1 = entry1.Identifier;
            Guid id2 = entry2.Identifier;
            Guid idFail = entryFail.Identifier;

            List<CSEntryChange> entries = new List<CSEntryChange> { entry1, entry2, entryFail };

            IList<CSEntryChangeResult> results = await orchestrator.PutAsync(entries, CancellationToken.None);

            HashSet<Guid> expectedIds = new HashSet<Guid> { id1, id2, idFail };

            foreach (CSEntryChangeResult result in results)
            {
                Assert.IsTrue(
                    expectedIds.Contains(result.Identifier),
                    string.Format("Unexpected result identifier '{0}'", result.Identifier));
            }

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// The "failme" entry produces a non-Success error code; other entries produce Success.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_PutAsync_FailmeEntry_NonSuccess_UserEntries_Success()
        {
            WorkerHost host = LoadHost();
            ExportContext context = BuildExportContext();
            Ecma2ExportOrchestrator orchestrator = new Ecma2ExportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            CSEntryChange entryUser1 = BuildEntry("user");
            CSEntryChange entryFail = BuildEntry("failme");
            CSEntryChange entryUser2 = BuildEntry("user");

            Guid idUser1 = entryUser1.Identifier;
            Guid idFail = entryFail.Identifier;
            Guid idUser2 = entryUser2.Identifier;

            List<CSEntryChange> entries = new List<CSEntryChange> { entryUser1, entryFail, entryUser2 };

            IList<CSEntryChangeResult> results = await orchestrator.PutAsync(entries, CancellationToken.None);

            Assert.AreEqual(3, results.Count, "Expected 3 results for 3 entries");

            // Build a lookup for flexible assertion.
            Dictionary<Guid, CSEntryChangeResult> byId = new Dictionary<Guid, CSEntryChangeResult>();

            foreach (CSEntryChangeResult r in results)
            {
                byId[r.Identifier] = r;
            }

            // failme must be non-Success
            Assert.IsTrue(byId.ContainsKey(idFail), "No result for failme entry");
            Assert.AreNotEqual(
                MAExportError.Success,
                byId[idFail].ErrorCode,
                "failme entry must produce a non-Success error code");

            // user entries must be Success
            Assert.IsTrue(byId.ContainsKey(idUser1), "No result for user1 entry");
            Assert.AreEqual(
                MAExportError.Success,
                byId[idUser1].ErrorCode,
                "user1 entry must have Success error code");

            Assert.IsTrue(byId.ContainsKey(idUser2), "No result for user2 entry");
            Assert.AreEqual(
                MAExportError.Success,
                byId[idUser2].ErrorCode,
                "user2 entry must have Success error code");

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// An empty batch returns an empty result list without error.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_PutAsync_EmptyBatch_ReturnsEmptyList()
        {
            WorkerHost host = LoadHost();
            ExportContext context = BuildExportContext();
            Ecma2ExportOrchestrator orchestrator = new Ecma2ExportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            IList<CSEntryChangeResult> results = await orchestrator.PutAsync(
                new List<CSEntryChange>(),
                CancellationToken.None);

            Assert.AreEqual(0, results.Count, "Empty batch must return empty result list");

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// CloseAsync completes without error after an open and put.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_CloseAsync_CompletesWithoutError()
        {
            WorkerHost host = LoadHost();
            ExportContext context = BuildExportContext();
            Ecma2ExportOrchestrator orchestrator = new Ecma2ExportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            await orchestrator.PutAsync(
                new List<CSEntryChange> { BuildEntry("user") },
                CancellationToken.None);

            // Should not throw.
            await orchestrator.CloseAsync();
        }
    }
}
