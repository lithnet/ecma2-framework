using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            try
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

                if (receiver.HasConfigProvider && receiver.HasConfigAttributes)
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2005", "Multiple configuration implementations were detected", "An implementation of IConfigurationParametersProvider was found, along with one or more classes decorated with configuration attributes. Either configure all your parameters manually using the IConfigurationParametersProvider interface, or use the automatic implementation provided by the [*Configuration] attributes", "Ecma2Framework", DiagnosticSeverity.Warning, true), Location.None));
                }

                foreach (var diagnostic in receiver.Diagnostics)
                {
                    hasErrors |= diagnostic.Severity == DiagnosticSeverity.Error;
                    context.ReportDiagnostic(diagnostic);
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

                this.AddConfigParameterMapping(context, receiver);
                this.AddGeneratedConfigParametersSource(context, receiver);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2000", "An unexpected error occurred", $"An unexpected error occurred: {ex}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
            }
        }

        private void AddConfigParameterMapping(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var mapping in receiver.Mapping)
            {
                builder.AppendLine($"{{ \"{mapping.Key}\", \"{mapping.Value}\"}},");
            }

            var configMappingText = this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2ConfigParameterMapping.txt").Replace("%MAPPEDPROPERTIES%", builder.ToString());

            builder.Clear();

            foreach (var service in receiver.ServicesToRegister)
            {
                builder.AppendLine(service);
            }

            foreach (var service in receiver.DiscoveredConfigClasses.SelectMany(t => t.ServicesToRegister))
            {
                builder.AppendLine(service);
            }

            if (receiver.DiscoveredConfigClasses.Count > 0)
            {
                builder.AppendLine("services.AddSingleton<IConfigParametersProvider, Ecma2GeneratedConfigParametersProvider>();");
            }

            configMappingText = configMappingText.Replace("%SERVICEREGISTRATIONS%", builder.ToString());

            context.AddSource("Ecma2ConfigParameterMapping.g.cs", SourceText.From(configMappingText, Encoding.UTF8));
        }

        private void AddGeneratedConfigParametersSource(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            if (receiver.DiscoveredConfigClasses.Count == 0)
            {
                return;
            }

            var parametersProviderText = this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2GeneratedConfigParametersProvider.txt");

            foreach (var configClass in receiver.DiscoveredConfigClasses)
            {
                string paramsPlaceholder = configClass.Page switch
                {
                    ConfigParameterPage.Capabilities => "//%CAPABILITIESPARAMS%",
                    ConfigParameterPage.Connectivity => "//%CONNECTIVITYPARAMS%",
                    ConfigParameterPage.Global => "//%GLOBALPARAMS%",
                    ConfigParameterPage.Partition => "//%PARTITIONPARAMS%",
                    ConfigParameterPage.RunStep => "//%RUNSTEPPARAMS%",
                    ConfigParameterPage.Schema => "//%SCHEMAPARAMS%",
                    _ => throw new System.NotImplementedException(),
                };

                string validationPlaceholder = configClass.Page switch
                {
                    ConfigParameterPage.Capabilities => "//%CAPABILITIESVALIDATION%",
                    ConfigParameterPage.Connectivity => "//%CONNECTIVITYVALIDATION%",
                    ConfigParameterPage.Global => "//%GLOBALVALIDATION%",
                    ConfigParameterPage.Partition => "//%PARTITIONVALIDATION%",
                    ConfigParameterPage.RunStep => "//%RUNSTEPVALIDATION%",
                    ConfigParameterPage.Schema => "//%SCHEMAVALIDATION%",
                    _ => throw new System.NotImplementedException(),
                };

                parametersProviderText = parametersProviderText.Replace(paramsPlaceholder, this.GenerateStringBlock(configClass.ParametersToAdd));
                parametersProviderText = parametersProviderText.Replace(validationPlaceholder, $"result = OptionsValidator.ValidateObject(this.serviceProvider.GetService<IOptions<{configClass.ClassName}>>()?.Value, this.serviceProvider);");
            }

            context.AddSource("Ecma2GeneratedConfigParametersProvider.g.cs", SourceText.From(parametersProviderText, Encoding.UTF8));
        }
        private string GenerateStringBlock(List<string> items)
        {
            if (items == null)
            {
                return null;
            }

            StringBuilder builder = new StringBuilder();
            foreach (var line in items)
            {
                builder.AppendLine(line);
            }

            return builder.ToString();
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
