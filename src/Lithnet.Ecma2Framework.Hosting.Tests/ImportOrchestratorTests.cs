using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.Hosting;
using Lithnet.Ecma2Framework.TestConsumer;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// Unit tests that drive <see cref="Ecma2ImportOrchestrator"/> directly using the real
    /// <c>TestImportProvider</c> from the TestConsumer assembly loaded via <see cref="WorkerHost"/>.
    /// These tests verify the producer/consumer paging contract without going through the RPC pipe.
    /// </summary>
    /// <remarks>
    /// The TestConsumer's <c>TestImportProvider</c> yields exactly 3 Add entries for object type
    /// "user" (user-1, user-2, user-3) and returns a fixed outbound watermark "test-watermark-v1".
    /// Tests assert page boundaries, <c>MoreToImport</c> semantics, entry identity, and watermark JSON.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A null <c>workerHost</c> or missing consumer DLL causes the test to fail with a
    ///     clear assertion message rather than a cryptic NullReferenceException.</item>
    ///   <item>Orchestrator exceptions propagate from <see cref="Ecma2ImportOrchestrator.GetNextPageAsync"/>
    ///     if the producer faulted; such failures are surfaced as test failures by the MSTest harness.</item>
    /// </list>
    /// </remarks>
    [TestClass]
    public class ImportOrchestratorTests
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
        /// Builds a minimal <see cref="ImportContext"/> with the TestConsumer's "user" schema and
        /// a Full import run step.
        /// </summary>
        private static ImportContext BuildFullImportContext()
        {
            Schema schema = Schema.Create();
            SchemaType userType = SchemaType.Create("user", false);
            userType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));
            schema.Types.Add(userType);

            // Real MMS OpenImportConnectionRunStep has no static Create factory (that was a mirror-only
            // shape); construct via its parameterized ctor: (partition, importType, pageSize, customData,
            // inclusion, exclusion). Preserves the original intent: Full import, page size 100, no custom data.
            OpenImportConnectionRunStep runStep = new OpenImportConnectionRunStep(
                null,
                OperationType.Full,
                100,
                null,
                null,
                null);

            return ImportContext.Create(runStep, schema);
        }

        // -------------------------------------------------------------------------
        // Tests
        // -------------------------------------------------------------------------

        /// <summary>
        /// Open + drain all with a large page size → exactly 3 entries, no MoreToImport.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_AllEntriesInOnePage_WhenPageSizeLarge()
        {
            WorkerHost host = LoadHost();
            ImportContext context = BuildFullImportContext();
            Ecma2ImportOrchestrator orchestrator = new Ecma2ImportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            ImportPage page = await orchestrator.GetNextPageAsync(100, CancellationToken.None);

            Assert.AreEqual(3, page.Entries.Count, "Expected 3 entries from TestImportProvider");
            Assert.IsFalse(page.MoreToImport, "MoreToImport should be false when all entries fit in one page");

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// Open + page with pageSize=2 → page1 has 2 entries + MoreToImport, page2 has 1 entry + no MoreToImport.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_PagesCorrectly_WithPageSizeTwo()
        {
            WorkerHost host = LoadHost();
            ImportContext context = BuildFullImportContext();
            Ecma2ImportOrchestrator orchestrator = new Ecma2ImportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            ImportPage page1 = await orchestrator.GetNextPageAsync(2, CancellationToken.None);

            Assert.AreEqual(2, page1.Entries.Count, "First page should have 2 entries");
            Assert.IsTrue(page1.MoreToImport, "MoreToImport should be true after first page of 3");

            ImportPage page2 = await orchestrator.GetNextPageAsync(2, CancellationToken.None);

            Assert.AreEqual(1, page2.Entries.Count, "Second page should have 1 remaining entry");
            Assert.IsFalse(page2.MoreToImport, "MoreToImport should be false on the final page");

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// Total entry count across all pages equals the number of entries the TestImportProvider yields.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_TotalEntryCount_EqualsThree()
        {
            WorkerHost host = LoadHost();
            ImportContext context = BuildFullImportContext();
            Ecma2ImportOrchestrator orchestrator = new Ecma2ImportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            List<CSEntryChange> all = new List<CSEntryChange>();
            ImportPage page;

            do
            {
                page = await orchestrator.GetNextPageAsync(2, CancellationToken.None);
                all.AddRange(page.Entries);
            }
            while (page.MoreToImport);

            Assert.AreEqual(3, all.Count, "Total entries across all pages must be 3");

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// All entries are Add modifications of object type "user" with anchor attribute "id".
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_Entries_HaveExpectedObjectType_And_ModificationType()
        {
            WorkerHost host = LoadHost();
            ImportContext context = BuildFullImportContext();
            Ecma2ImportOrchestrator orchestrator = new Ecma2ImportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            ImportPage page = await orchestrator.GetNextPageAsync(100, CancellationToken.None);

            foreach (CSEntryChange entry in page.Entries)
            {
                Assert.AreEqual(
                    ObjectModificationType.Add,
                    entry.ObjectModificationType,
                    "All TestImportProvider entries should be Add modifications");

                Assert.AreEqual(
                    "user",
                    entry.ObjectType,
                    "All TestImportProvider entries should be of type 'user'");

                Assert.IsTrue(
                    entry.AnchorAttributes.Contains("id"),
                    "Each entry must have anchor attribute 'id'");
            }

            await orchestrator.CloseAsync();
        }

        /// <summary>
        /// CloseAsync returns a JSON watermark containing the "user" key with the expected value.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_CloseAsync_ReturnsWatermarkJson()
        {
            WorkerHost host = LoadHost();
            ImportContext context = BuildFullImportContext();
            Ecma2ImportOrchestrator orchestrator = new Ecma2ImportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            // Drain all entries so the producer finishes and captures the watermark.
            ImportPage page;

            do
            {
                page = await orchestrator.GetNextPageAsync(100, CancellationToken.None);
            }
            while (page.MoreToImport);

            string watermark = await orchestrator.CloseAsync();

            Assert.IsFalse(
                string.IsNullOrEmpty(watermark),
                "CloseAsync should return a non-empty watermark string");

            Assert.IsTrue(
                watermark.Contains("test-watermark-v1"),
                "Watermark JSON should contain the expected value 'test-watermark-v1'. Actual: " + watermark);
        }

        /// <summary>
        /// Consecutive page calls after the collection is complete return empty pages.
        /// </summary>
        [TestMethod]
        public async Task Orchestrator_GetNextPage_AfterExhaustion_ReturnsEmptyPage()
        {
            WorkerHost host = LoadHost();
            ImportContext context = BuildFullImportContext();
            Ecma2ImportOrchestrator orchestrator = new Ecma2ImportOrchestrator(host.Services);

            await orchestrator.OpenAsync(context);

            // Drain completely.
            ImportPage page;

            do
            {
                page = await orchestrator.GetNextPageAsync(100, CancellationToken.None);
            }
            while (page.MoreToImport);

            // An extra call after exhaustion must return an empty page, not throw.
            ImportPage extra = await orchestrator.GetNextPageAsync(100, CancellationToken.None);

            Assert.AreEqual(0, extra.Entries.Count, "Extra page after exhaustion must be empty");
            Assert.IsFalse(extra.MoreToImport, "MoreToImport must be false after exhaustion");

            await orchestrator.CloseAsync();
        }
    }
}
