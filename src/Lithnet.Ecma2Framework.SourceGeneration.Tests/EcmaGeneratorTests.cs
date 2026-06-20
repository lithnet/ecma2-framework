using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    [TestClass]
    public class EcmaGeneratorTests
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

        private const string ExportProviderClass = @"
public class MyExportProvider : Lithnet.Ecma2Framework.IObjectExportProvider
{
    public System.Threading.Tasks.Task InitializeAsync(Lithnet.Ecma2Framework.ExportContext context) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<bool> CanExportAsync(Microsoft.MetadirectoryServices.CSEntryChange csentry) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.CSEntryChangeResult> PutCSEntryChangeAsync(Microsoft.MetadirectoryServices.CSEntryChange csentry, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
}
";

        private static Dictionary<string, string> BuildProperties(string managementAgentName)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            if (managementAgentName != null)
            {
                properties["build_property.Ecma2ManagementAgentName"] = managementAgentName;
            }

            return properties;
        }

        private static bool HasDiagnostic(ImmutableArray<Diagnostic> diagnostics, string id)
        {
            return diagnostics.Any(d => d.Id == id);
        }

        private static bool HasError(ImmutableArray<Diagnostic> diagnostics)
        {
            return diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        }

        [TestMethod]
        public void ValidMinimalConsumer_WorkerRole_ProducesNoErrors()
        {
            string source = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsFalse(HasError(diagnostics), "The valid minimal consumer should not produce any error-severity diagnostics. Diagnostics: " + string.Join(", ", diagnostics.Select(d => d.Id)));

            // Guard against the references being incomplete: if discovery could not bind the framework interfaces,
            // the providers would read as missing (ECMA2002/2003/2004) or the generator would throw (ECMA2000).
            // Their absence confirms discovery genuinely resolved the providers.
            Assert.IsFalse(HasDiagnostic(diagnostics, "ECMA2000"), "The generator must not report an unexpected error for a valid consumer.");
            Assert.IsFalse(HasDiagnostic(diagnostics, "ECMA2002"), "The schema provider must be discovered.");
            Assert.IsFalse(HasDiagnostic(diagnostics, "ECMA2003"), "The capabilities provider must be discovered.");
            Assert.IsFalse(HasDiagnostic(diagnostics, "ECMA2004"), "The import provider must be discovered.");
        }

        [TestMethod]
        public void MissingStartup_ReportsEcma2001()
        {
            string source = SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2001"));
        }

        [TestMethod]
        public void MissingSchemaProvider_ReportsEcma2002()
        {
            string source = StartupClass + CapabilitiesProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2002"));
        }

        [TestMethod]
        public void MissingCapabilitiesProvider_ReportsEcma2003()
        {
            string source = StartupClass + SchemaProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2003"));
        }

        [TestMethod]
        public void NoImportOrExportProvider_ReportsEcma2004()
        {
            string source = StartupClass + SchemaProviderClass + CapabilitiesProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2004"));
        }

        [TestMethod]
        public void ConfigProviderAndConfigAttributesPresent_ReportsEcma2005Warning()
        {
            const string configProviderAndAttributes = @"
[Lithnet.Ecma2Framework.GlobalConfiguration(""Ecma:Global"")]
public class MyConfig
{
    [Lithnet.Ecma2Framework.StringParameter(""Server"")]
    public string Server { get; set; }
}

public class MyConfigParametersProvider : Lithnet.Ecma2Framework.IConfigParametersProvider
{
    public System.Threading.Tasks.Task GetCapabilitiesConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters existingParameters, System.Collections.Generic.IList<Microsoft.MetadirectoryServices.ConfigParameterDefinition> newDefinitions) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task GetConnectivityConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters existingParameters, System.Collections.Generic.IList<Microsoft.MetadirectoryServices.ConfigParameterDefinition> newDefinitions) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task GetGlobalConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters existingParameters, System.Collections.Generic.IList<Microsoft.MetadirectoryServices.ConfigParameterDefinition> newDefinitions) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task GetPartitionConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters existingParameters, System.Collections.Generic.IList<Microsoft.MetadirectoryServices.ConfigParameterDefinition> newDefinitions) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task GetRunStepConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters existingParameters, System.Collections.Generic.IList<Microsoft.MetadirectoryServices.ConfigParameterDefinition> newDefinitions) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task GetSchemaConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters existingParameters, System.Collections.Generic.IList<Microsoft.MetadirectoryServices.ConfigParameterDefinition> newDefinitions, int pageNumber) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.ParameterValidationResult> ValidateCapabilitiesConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.ParameterValidationResult> ValidateConnectivityConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.ParameterValidationResult> ValidateGlobalConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.ParameterValidationResult> ValidatePartitionConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.ParameterValidationResult> ValidateRunStepConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.ParameterValidationResult> ValidateSchemaConfigParametersAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters, int pageNumber) => throw new System.NotImplementedException();
}
";

            string source = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass + configProviderAndAttributes;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Diagnostic ecma2005 = diagnostics.FirstOrDefault(d => d.Id == "ECMA2005");
            Assert.IsNotNull(ecma2005, "Expected ECMA2005 to be reported.");
            Assert.AreEqual(DiagnosticSeverity.Warning, ecma2005.Severity);
        }

        [TestMethod]
        public void TwoStartupClasses_ReportsEcma2010()
        {
            const string secondStartup = @"
public class MySecondStartup : Lithnet.Ecma2Framework.IEcmaStartup
{
    public void Configure(Microsoft.Extensions.Configuration.IConfigurationBuilder builder) { }
    public void SetupServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Lithnet.Ecma2Framework.IConfigParameters configParameters) { }
}
";

            string source = StartupClass + secondStartup + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2010"));
        }

        [TestMethod]
        public void DuplicateParameterName_ReportsEcma2007()
        {
            const string duplicateConfig = @"
[Lithnet.Ecma2Framework.GlobalConfiguration(""Ecma:Global"")]
public class MyConfig
{
    [Lithnet.Ecma2Framework.StringParameter(""Server"")]
    public string Server { get; set; }

    [Lithnet.Ecma2Framework.StringParameter(""Server"")]
    public string ServerAgain { get; set; }
}
";

            string source = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass + duplicateConfig;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("Test.MA"));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2007"));
        }

        [TestMethod]
        public void MissingManagementAgentName_ReportsEcma2011()
        {
            string source = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties(null));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2011"));
        }

        [TestMethod]
        public void WhitespaceManagementAgentName_ReportsEcma2011()
        {
            string source = StartupClass + SchemaProviderClass + CapabilitiesProviderClass + ImportProviderClass;

            ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.RunGenerator(source, BuildProperties("   "));

            Assert.IsTrue(HasDiagnostic(diagnostics, "ECMA2011"));
        }
    }
}
