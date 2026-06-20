using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    /// <summary>
    /// Proves the Design C referenced-assembly discovery path: the generator runs in a HOST compilation that has no
    /// consumer source of its own, only a metadata reference to the compiled consumer assembly. When the
    /// <c>Ecma2ConsumerAssemblyName</c> build property names that reference, the generator must discover the
    /// consumer's public startup/providers from it and emit the worker Main. A non-public participant must be
    /// reported as ECMA2013 (a guided "make it public" error), not the misleading ECMA2001 "no startup".
    /// </summary>
    [TestClass]
    public class ReferencedAssemblyDiscoveryTests
    {
        [TestMethod]
        public void Generator_discovers_startup_from_referenced_consumer_assembly_and_emits_main()
        {
            string consumerSource = TestConsumerSources.MinimalValidConsumer("Acme.DemoMa");

            Dictionary<string, string> buildProperties = new Dictionary<string, string>
            {
                { "build_property.Ecma2ManagementAgentName", "Acme.DemoMa.Ecma2" },
                { "build_property.Ecma2ConsumerAssemblyName", "Acme.DemoMa" },
            };

            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAgainstReferencedConsumer(
                consumerSource, "Acme.DemoMa", buildProperties);

            foreach (GeneratorRunResult runResult in result.Results)
            {
                foreach (Diagnostic diagnostic in runResult.Diagnostics)
                {
                    Assert.AreNotEqual(DiagnosticSeverity.Error, diagnostic.Severity, diagnostic.GetMessage());
                }
            }

            string workerProgram = GeneratorTestHarness.GetGeneratedSource(result, "WorkerProgram.g.cs");
            Assert.IsNotNull(workerProgram, "the worker Main was not generated from the referenced consumer assembly");
            Assert.IsTrue(workerProgram.Contains("Acme.DemoMa.DemoStartup"), "the generated Main does not reference the consumer's startup type. Source: " + workerProgram);
        }

        [TestMethod]
        public void Generator_discovers_internal_providers_from_referenced_consumer_and_emits_main()
        {
            // The real-world shape (e.g. the Okta MA): the startup is public but the provider implementations
            // are internal, because the generated host code never names a provider directly (it resolves them
            // via DI). The generator must DISCOVER those internal providers across the assembly boundary so the
            // worker-role existence checks (ECMA2002-2005) pass and the worker Main is emitted without error.
            // Only the startup (named by the generated Main) needs to be public.
            string consumerSource = TestConsumerSources.MinimalConsumerWithInternalProviders("Acme.DemoMa");

            Dictionary<string, string> buildProperties = new Dictionary<string, string>
            {
                { "build_property.Ecma2ManagementAgentName", "Acme.DemoMa.Ecma2" },
                { "build_property.Ecma2ConsumerAssemblyName", "Acme.DemoMa" },
            };

            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAgainstReferencedConsumer(
                consumerSource, "Acme.DemoMa", buildProperties);

            foreach (GeneratorRunResult runResult in result.Results)
            {
                foreach (Diagnostic diagnostic in runResult.Diagnostics)
                {
                    Assert.AreNotEqual(DiagnosticSeverity.Error, diagnostic.Severity, diagnostic.GetMessage());
                }
            }

            string workerProgram = GeneratorTestHarness.GetGeneratedSource(result, "WorkerProgram.g.cs");
            Assert.IsNotNull(workerProgram, "the worker Main was not generated when the consumer's providers are internal");
            Assert.IsTrue(workerProgram.Contains("Acme.DemoMa.DemoStartup"), "the generated Main does not reference the consumer's startup type. Source: " + workerProgram);
        }

        [TestMethod]
        public void Generator_reports_ECMA2013_when_referenced_startup_is_not_public()
        {
            // The only difference from the valid fixture is that DemoStartup is internal. From a separate host
            // assembly an internal participant cannot be named, so the generator must guide the implementor with
            // ECMA2013 specifically - not the misleading ECMA2001 "no startup found".
            string consumerSource = TestConsumerSources.MinimalConsumerWithInternalStartup("Acme.DemoMa");

            Dictionary<string, string> buildProperties = new Dictionary<string, string>
            {
                { "build_property.Ecma2ManagementAgentName", "Acme.DemoMa.Ecma2" },
                { "build_property.Ecma2ConsumerAssemblyName", "Acme.DemoMa" },
            };

            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAgainstReferencedConsumer(
                consumerSource, "Acme.DemoMa", buildProperties);

            List<Diagnostic> diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();

            // The guided diagnostic for a non-public participant is ECMA2013 specifically - a "make this type public"
            // error naming the offending type - not just any error. Its presence is the contract this test protects:
            // an implementor whose startup is internal is told exactly why and how to fix it.
            Diagnostic ecma2013 = diagnostics.FirstOrDefault(d => d.Id == "ECMA2013");
            Assert.IsNotNull(ecma2013, "Expected ECMA2013 for the non-public startup. Diagnostics: " + string.Join("; ", diagnostics.Select(d => d.Id + ": " + d.GetMessage())));
            Assert.IsTrue(ecma2013.GetMessage().Contains("Acme.DemoMa.DemoStartup"), "ECMA2013 must name the offending type. Message: " + ecma2013.GetMessage());

            // ECMA2013 fires INSTEAD of ECMA2001: the non-public startup was discovered and flagged, so the
            // contradictory "a startup class could not be found" must be suppressed - the implementor gets one
            // accurate, actionable diagnostic, not a misleading second one.
            Diagnostic ecma2001 = diagnostics.FirstOrDefault(d => d.Id == "ECMA2001");
            Assert.IsNull(ecma2001, "ECMA2001 must be suppressed when ECMA2013 already flagged the non-public startup. Diagnostics: " + string.Join("; ", diagnostics.Select(d => d.Id + ": " + d.GetMessage())));
        }

        [TestMethod]
        public void Generator_reports_ECMA2013_when_referenced_startup_is_public_but_nested_in_nonpublic_type()
        {
            // The startup class is itself declared public, but it is nested inside an internal outer type. Its own
            // modifier passes a naive public check, yet from a separate host assembly the type is NOT reachable, so
            // naming it in the generated Main would fail at host compile with CS0122. The generator must flag this on
            // EFFECTIVE (cross-assembly) accessibility - ECMA2013 present, ECMA2001 suppressed - and must NOT emit a
            // WorkerProgram.g.cs that names the inaccessible type. Every other required participant is public and
            // top-level, so the nested startup is the only issue.
            string consumerSource = TestConsumerSources.MinimalConsumerWithNestedPublicStartup("Acme.DemoMa");

            Dictionary<string, string> buildProperties = new Dictionary<string, string>
            {
                { "build_property.Ecma2ManagementAgentName", "Acme.DemoMa.Ecma2" },
                { "build_property.Ecma2ConsumerAssemblyName", "Acme.DemoMa" },
            };

            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAgainstReferencedConsumer(
                consumerSource, "Acme.DemoMa", buildProperties);

            List<Diagnostic> diagnostics = result.Results.SelectMany(r => r.Diagnostics).ToList();

            Diagnostic ecma2013 = diagnostics.FirstOrDefault(d => d.Id == "ECMA2013");
            Assert.IsNotNull(ecma2013, "Expected ECMA2013 for the public-but-nested-in-nonpublic startup. Diagnostics: " + string.Join("; ", diagnostics.Select(d => d.Id + ": " + d.GetMessage())));
            Assert.IsTrue(ecma2013.GetMessage().Contains("Acme.DemoMa.Outer.DemoStartup"), "ECMA2013 must name the offending nested type. Message: " + ecma2013.GetMessage());

            Diagnostic ecma2001 = diagnostics.FirstOrDefault(d => d.Id == "ECMA2001");
            Assert.IsNull(ecma2001, "ECMA2001 must be suppressed when ECMA2013 already flagged the nested startup. Diagnostics: " + string.Join("; ", diagnostics.Select(d => d.Id + ": " + d.GetMessage())));

            string workerProgram = GeneratorTestHarness.GetGeneratedSource(result, "WorkerProgram.g.cs");
            Assert.IsNull(workerProgram, "the worker Main must NOT be emitted naming an inaccessible nested startup type");
        }
    }
}
