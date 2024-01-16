using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Lithnet.Ecma2Framework
{
    [Generator]
    public class EcmaGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new Ecma2InitializerSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not Ecma2InitializerSyntaxReceiver receiver)
            {
                return;
            }

            bool hasErrors = false;

            if (!receiver.HasBootstrapper || string.IsNullOrWhiteSpace(receiver.BootstrapperClassName))
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2001", "Could not find the bootstrapper implementation", $"A bootstrapper class could not be found. Please generate a class that implements from IEcmaBootstrapper", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (!receiver.HasSchemaProvider)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2002", "Could not find the schema provider implementation", $"A schema provider class could not be found. Please generate a class that implements from ISchemaProvider", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (!receiver.HasCapabilityProvider)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2003", "Could not find the capabilities provider implementation", $"A capabilities provider class could not be found. Please generate a class that implements from ICapabilitiesProvider", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (!receiver.HasObjectExportProvider && !receiver.HasObjectImportProvider)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2004", "Could not find the object import or export provider implementation", $"An object provider class could not be found. Please generate at least one class that implements from IObjectExportProvider or IObjectImportProvider", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (hasErrors)
            {
                return;
            }

            var bootstrapperText = this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2Bootstrapper.txt").Replace("%CLASSNAME%", receiver.BootstrapperClassName);
            context.AddSource("Ecma2Bootstrapper.g.cs", SourceText.From(bootstrapperText, Encoding.UTF8));
            context.AddSource("Ecma2ImportImplementation.g.cs", SourceText.From(this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2ImportImplementation.txt"), Encoding.UTF8));
            context.AddSource("Ecma2ExportImplementation.g.cs", SourceText.From(this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2ExportImplementation.txt"), Encoding.UTF8));
            context.AddSource("Ecma2PasswordImplementation.g.cs", SourceText.From(this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2PasswordImplementation.txt"), Encoding.UTF8));
            context.AddSource("Ecma2Implementation.g.cs", SourceText.From(this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2Implementation.txt"), Encoding.UTF8));
        }

        private string GetResource(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
