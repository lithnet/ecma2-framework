using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    /// <summary>
    /// Assembles a C# compilation from a source string and drives the <see cref="EcmaGenerator"/> against it,
    /// returning the diagnostics the generator reported. The references include the core BCL plus the v3 framework
    /// assembly, so the framework types the generator discovers resolve by metadata name during discovery.
    /// </summary>
    internal static class GeneratorTestHarness
    {
        public static ImmutableArray<Diagnostic> RunGenerator(string source, Dictionary<string, string> buildProperties)
        {
            return RunGeneratorAndGetResult(source, buildProperties).Diagnostics;
        }

        /// <summary>
        /// Drives the generator against the supplied source and returns the full run result, exposing the
        /// generated sources (by hint name) and their text so emission can be asserted directly.
        /// </summary>
        public static GeneratorDriverRunResult RunGeneratorAndGetResult(string source, Dictionary<string, string> buildProperties)
        {
            CSharpCompilation compilation = CSharpCompilation.Create(
                "TestConsumerAssembly",
                new[] { CSharpSyntaxTree.ParseText(source) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            EcmaGenerator generator = new EcmaGenerator();

            TestAnalyzerConfigOptionsProvider optionsProvider = new TestAnalyzerConfigOptionsProvider(buildProperties);

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new ISourceGenerator[] { generator },
                additionalTexts: ImmutableArray<AdditionalText>.Empty,
                parseOptions: null,
                optionsProvider: optionsProvider);

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out _);

            return driver.GetRunResult();
        }

        /// <summary>
        /// Compiles <paramref name="consumerSource"/> to an in-memory assembly named
        /// <paramref name="consumerAssemblyName"/>, then runs the generator against a SEPARATE compilation that has
        /// no source of its own but references that consumer assembly. This exercises the Design C path where the
        /// host project discovers the consumer's startup/providers from a referenced assembly's metadata. The
        /// <c>Ecma2ConsumerAssemblyName</c> build property is set so the generator knows which reference to scan.
        /// </summary>
        public static GeneratorDriverRunResult RunGeneratorAgainstReferencedConsumer(
            string consumerSource,
            string consumerAssemblyName,
            Dictionary<string, string> buildProperties)
        {
            CSharpCompilation consumerCompilation = CSharpCompilation.Create(
                consumerAssemblyName,
                new[] { CSharpSyntaxTree.ParseText(consumerSource) },
                GetReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            using MemoryStream peStream = new MemoryStream();
            Microsoft.CodeAnalysis.Emit.EmitResult emitResult = consumerCompilation.Emit(peStream);
            if (!emitResult.Success)
            {
                throw new InvalidOperationException(
                    "The consumer test source failed to compile: " +
                    string.Join("; ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
            }

            peStream.Position = 0;
            MetadataReference consumerReference = MetadataReference.CreateFromImage(peStream.ToArray());

            List<MetadataReference> hostReferences = new List<MetadataReference>(GetReferences());
            hostReferences.Add(consumerReference);

            CSharpCompilation hostCompilation = CSharpCompilation.Create(
                "HostAssembly",
                Array.Empty<SyntaxTree>(),
                hostReferences,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));

            EcmaGenerator generator = new EcmaGenerator();
            TestAnalyzerConfigOptionsProvider optionsProvider = new TestAnalyzerConfigOptionsProvider(buildProperties);

            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                new ISourceGenerator[] { generator },
                additionalTexts: ImmutableArray<AdditionalText>.Empty,
                parseOptions: null,
                optionsProvider: optionsProvider);

            driver = driver.RunGeneratorsAndUpdateCompilation(hostCompilation, out _, out _);
            return driver.GetRunResult();
        }

        /// <summary>
        /// Returns the text of the generated source with the given hint name suffix (e.g. "WorkerProgram.g.cs"),
        /// or null when no such source was emitted.
        /// </summary>
        public static string GetGeneratedSource(GeneratorDriverRunResult result, string hintNameSuffix)
        {
            foreach (GeneratorRunResult generatorResult in result.Results)
            {
                foreach (GeneratedSourceResult generated in generatorResult.GeneratedSources)
                {
                    if (generated.HintName.EndsWith(hintNameSuffix, StringComparison.OrdinalIgnoreCase))
                    {
                        return generated.SourceText.ToString();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Compiles the consumer source together with the generator's emitted worker sources as a console
        /// application, against the full runtime surface plus the framework, object-model, and worker assemblies.
        /// This proves the generated Main and config providers are valid C# against the real types rather than
        /// merely matching strings.
        /// </summary>
        public static CSharpCompilation CompileConsumerWithGeneratedSources(string consumerSource, IEnumerable<string> generatedSources)
        {
            List<SyntaxTree> trees = new List<SyntaxTree>();
            trees.Add(CSharpSyntaxTree.ParseText(consumerSource));

            foreach (string generated in generatedSources)
            {
                trees.Add(CSharpSyntaxTree.ParseText(generated));
            }

            List<MetadataReference> references = new List<MetadataReference>(GetReferences());

            // The worker assembly carries WorkerEntryPoint, which the generated Main calls into.
            references.Add(MetadataReference.CreateFromFile(typeof(Lithnet.Ecma2Framework.Hosting.WorkerEntryPoint).Assembly.Location));

            // Microsoft.Extensions.Options is referenced by the generated config-parameters provider (IOptions<T>).
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Options.IOptions<object>).Assembly.Location));

            return CSharpCompilation.Create(
                "TestConsumerExe",
                trees,
                references,
                new CSharpCompilationOptions(OutputKind.ConsoleApplication));
        }

        private static IEnumerable<MetadataReference> GetReferences()
        {
            List<MetadataReference> references = new List<MetadataReference>();

            // The trusted platform assemblies give us the full BCL surface (System.Runtime, netstandard, etc.) that
            // the test source and the framework's public surface need to bind cleanly. Without these, the framework
            // types fail to resolve and GetTypeByMetadataName returns null inside discovery.
            string trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            if (!string.IsNullOrEmpty(trustedAssemblies))
            {
                foreach (string path in trustedAssemblies.Split(Path.PathSeparator))
                {
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    {
                        references.Add(MetadataReference.CreateFromFile(path));
                    }
                }
            }

            // The v3 framework assembly. The generator's discovery resolves IEcmaStartup, the provider interfaces,
            // and the configuration attributes from this reference.
            references.Add(MetadataReference.CreateFromFile(typeof(IEcmaStartup).Assembly.Location));

            // The object-model assembly carries the types the framework's public surface exposes (Schema,
            // MACapabilities, SchemaType, CSEntryChange, ConfigParameterDefinition, etc.). The consumer source must
            // bind cleanly for the semantic model to resolve declared symbols and their interfaces during discovery.
            references.Add(MetadataReference.CreateFromFile(typeof(Schema).Assembly.Location));

            // The framework's startup and provider signatures reference Microsoft.Extensions abstractions
            // (IConfigurationBuilder, IServiceCollection). These are not part of the shared runtime, so add them
            // explicitly so the consumer source binds without missing-reference noise.
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.Configuration.IConfigurationBuilder).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection).Assembly.Location));

            return references;
        }
    }
}
