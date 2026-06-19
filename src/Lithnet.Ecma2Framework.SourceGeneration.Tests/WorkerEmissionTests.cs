using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    /// <summary>
    /// Proves the worker-role emission added in the v3 codegen stage: the generated worker Main, the
    /// config-registration provider, and the config-parameters provider. The compilation-proof test
    /// additionally compiles the emitted sources against the real runtime surface to prove they bind.
    /// </summary>
    [TestClass]
    public class WorkerEmissionTests
    {
        private const string StartupClass = @"
public class MyStartup : Lithnet.Ecma2Framework.IEcmaStartup
{
    public void Configure(Microsoft.Extensions.Configuration.IConfigurationBuilder builder) { }
    public void SetupServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Lithnet.Ecma2Framework.IConfigParameters configParameters) { }
}
";

        private const string SchemaProviderClass = @"
public class MySchemaProvider : Lithnet.Ecma2Framework.ISchemaProvider
{
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.Schema> GetMmsSchemaAsync() => throw new System.NotImplementedException();
}
";

        private const string CapabilitiesProviderClass = @"
public class MyCapabilitiesProvider : Lithnet.Ecma2Framework.ICapabilitiesProvider
{
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.MACapabilities> GetCapabilitiesAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
}
";

        private const string ImportProviderClass = @"
public class MyImportProvider : Lithnet.Ecma2Framework.IObjectImportProvider
{
    public System.Threading.Tasks.Task InitializeAsync(Lithnet.Ecma2Framework.ImportContext context) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<bool> CanImportAsync(Microsoft.MetadirectoryServices.SchemaType type) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task GetCSEntryChangesAsync(Microsoft.MetadirectoryServices.SchemaType type, Lithnet.Ecma2Framework.ICSEntryChangeCollection csentryCollection, string incomingWatermark, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<string> GetOutboundWatermark(Microsoft.MetadirectoryServices.SchemaType type, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
}
";

        // A global configuration class with a plain string parameter and an encrypted string parameter.
        // The encrypted parameter must map to the "string-encrypted" data type in the registration provider.
        private const string GlobalConfigClass = @"
[Lithnet.Ecma2Framework.GlobalConfiguration(""Ecma:Global"")]
public class MyConfig
{
    [Lithnet.Ecma2Framework.StringParameter(""Server"")]
    public string Server { get; set; }

    [Lithnet.Ecma2Framework.EncryptedStringParameter(""Secret"")]
    public string Secret { get; set; }
}
";

        private const string ValidConsumerWithConfig = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass + GlobalConfigClass;

        private const string ValidConsumerNoConfig = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass;

        private static Dictionary<string, string> BuildProperties(string managementAgentName)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            if (managementAgentName != null)
            {
                properties["build_property.Ecma2ManagementAgentName"] = managementAgentName;
            }

            return properties;
        }

        [TestMethod]
        public void WorkerRole_WithConfigClasses_EmitsAllThreeSources()
        {
            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAndGetResult(ValidConsumerWithConfig, BuildProperties("Test.MA"));

            string workerProgram = GeneratorTestHarness.GetGeneratedSource(result, "WorkerProgram.g.cs");
            string registration = GeneratorTestHarness.GetGeneratedSource(result, "Ecma2ConfigRegistrationProvider.g.cs");
            string parameters = GeneratorTestHarness.GetGeneratedSource(result, "Ecma2GeneratedConfigParametersProvider.g.cs");

            Assert.IsNotNull(workerProgram, "WorkerProgram.g.cs must be emitted in the worker role.");
            Assert.IsNotNull(registration, "Ecma2ConfigRegistrationProvider.g.cs must be emitted in the worker role.");
            Assert.IsNotNull(parameters, "Ecma2GeneratedConfigParametersProvider.g.cs must be emitted when config classes are present.");

            Assert.IsTrue(workerProgram.Contains("WorkerEntryPoint.RunAsync"), "The generated Main must call WorkerEntryPoint.RunAsync.");
            Assert.IsTrue(workerProgram.Contains("new global::MyStartup()"), "The generated Main must instantiate the discovered startup class via its fully-qualified name. Source: " + workerProgram);
            Assert.IsTrue(workerProgram.Contains("new Lithnet.Ecma2Framework.Generated.Ecma2ConfigRegistrationProvider()"), "The generated Main must instantiate the generated registration provider.");

            // The encrypted-string parameter must be mapped to the "string-encrypted" data type, and the plain
            // string parameter to the section:property key form, in the registration provider's mapping tables.
            Assert.IsTrue(registration.Contains("\"string-encrypted\""), "The encrypted parameter must map to the string-encrypted data type. Source: " + registration);
            Assert.IsTrue(registration.Contains("\"Ecma:Global:Secret\""), "The encrypted parameter name must map to its section:property key. Source: " + registration);
            Assert.IsTrue(registration.Contains("\"Ecma:Global:Server\""), "The string parameter name must map to its section:property key. Source: " + registration);
        }

        [TestMethod]
        public void WorkerRole_EmitsMetadirectoryServicesResolver()
        {
            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAndGetResult(ValidConsumerNoConfig, BuildProperties("Test.MA"));

            string resolver = GeneratorTestHarness.GetGeneratedSource(result, "Ecma2MetadirectoryServicesResolver.g.cs");

            Assert.IsNotNull(resolver, "The MMS runtime resolver must be emitted in the worker role.");
            Assert.IsTrue(resolver.Contains("System.Runtime.CompilerServices.ModuleInitializer"), "The resolver must register via a module initializer so the handler is installed before Main is JIT-compiled. Source: " + resolver);
            Assert.IsTrue(resolver.Contains("AppDomain.CurrentDomain.AssemblyResolve"), "The resolver must install an AppDomain assembly-resolve handler (works on .NET Framework and .NET). Source: " + resolver);
            Assert.IsTrue(resolver.Contains("FIMSynchronizationService"), "The resolver must derive the MMS path from the MIM Synchronization Service registry key. Source: " + resolver);

            // On the net5+ test host the BCL already provides ModuleInitializerAttribute, so the polyfill must NOT
            // be emitted (emitting one would be a duplicate definition).
            Assert.IsNull(GeneratorTestHarness.GetGeneratedSource(result, "Ecma2ModuleInitializerAttribute.g.cs"), "The ModuleInitializerAttribute polyfill must NOT be emitted when the target framework already provides it.");
        }

        [TestMethod]
        public void WorkerRole_NoConfigClasses_OmitsConfigParametersProvider()
        {
            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAndGetResult(ValidConsumerNoConfig, BuildProperties("Test.MA"));

            Assert.IsNotNull(GeneratorTestHarness.GetGeneratedSource(result, "WorkerProgram.g.cs"), "WorkerProgram.g.cs must be emitted.");
            Assert.IsNotNull(GeneratorTestHarness.GetGeneratedSource(result, "Ecma2ConfigRegistrationProvider.g.cs"), "Ecma2ConfigRegistrationProvider.g.cs must be emitted.");
            Assert.IsNull(GeneratorTestHarness.GetGeneratedSource(result, "Ecma2GeneratedConfigParametersProvider.g.cs"), "Ecma2GeneratedConfigParametersProvider.g.cs must NOT be emitted when there are no config classes.");
        }

        [TestMethod]
        public void WorkerRole_GeneratedSources_CompileCleanlyAsConsoleApp()
        {
            GeneratorDriverRunResult result = GeneratorTestHarness.RunGeneratorAndGetResult(ValidConsumerWithConfig, BuildProperties("Test.MA"));

            List<string> generated = new List<string>
            {
                GeneratorTestHarness.GetGeneratedSource(result, "WorkerProgram.g.cs"),
                GeneratorTestHarness.GetGeneratedSource(result, "Ecma2ConfigRegistrationProvider.g.cs"),
                GeneratorTestHarness.GetGeneratedSource(result, "Ecma2GeneratedConfigParametersProvider.g.cs"),
                GeneratorTestHarness.GetGeneratedSource(result, "Ecma2MetadirectoryServicesResolver.g.cs"),
            };

            foreach (string source in generated)
            {
                Assert.IsNotNull(source, "All three worker sources must be present for the compilation proof.");
            }

            CSharpCompilation compilation = GeneratorTestHarness.CompileConsumerWithGeneratedSources(ValidConsumerWithConfig, generated);

            List<Diagnostic> errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

            Assert.AreEqual(0, errors.Count, "The generated worker sources must compile without errors against the real runtime surface. Errors: " + string.Join("; ", errors.Select(e => e.ToString())));

            Assert.IsNotNull(compilation.GetEntryPoint(System.Threading.CancellationToken.None), "The compiled consumer exe must have a single entry point (the generated Main).");
        }
    }
}
