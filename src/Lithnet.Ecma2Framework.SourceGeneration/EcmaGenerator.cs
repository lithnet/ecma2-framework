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
        private const string BuildPropertyManagementAgentName = "build_property.Ecma2ManagementAgentName";

        private const string BuildPropertyConsumerAssemblyName = "build_property.Ecma2ConsumerAssemblyName";

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

                // Design C: when the host build names a consumer assembly, discover the consumer's startup/providers
                // from that REFERENCED assembly's metadata (the host project has no consumer source of its own). When
                // the property is absent, this is a no-op and discovery is the in-source path only (back-compat).
                this.DiscoverFromReferencedConsumer(context, receiver);

                this.ExecuteWorkerRole(context, receiver);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2000", "An unexpected error occurred", $"An unexpected error occurred: {ex}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
            }
        }

        /// <summary>
        /// When the <c>Ecma2ConsumerAssemblyName</c> build property is set, locates that referenced assembly in the
        /// compilation and runs the receiver's symbol-based discovery over every type it declares. This is the
        /// Design C path: the host exe project references the consumer library, so the consumer's IEcmaStartup and
        /// provider implementations live in a referenced assembly rather than in the host's own source. When the
        /// property is absent the method returns immediately. When the property is set but the named assembly is not
        /// among the references, ECMA2012 is raised: a misconfigured host build must fail loud, not silently emit an
        /// MA with no providers.
        /// </summary>
        private void DiscoverFromReferencedConsumer(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(BuildPropertyConsumerAssemblyName, out string consumerAssemblyName) || string.IsNullOrWhiteSpace(consumerAssemblyName))
            {
                return;
            }

            IAssemblySymbol consumerAssembly = null;

            foreach (IAssemblySymbol referenced in context.Compilation.SourceModule.ReferencedAssemblySymbols)
            {
                if (string.Equals(referenced.Identity.Name, consumerAssemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    consumerAssembly = referenced;
                    break;
                }
            }

            if (consumerAssembly == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2012", "The consumer assembly could not be found", $"The host build named consumer assembly '{consumerAssemblyName}' (build property Ecma2ConsumerAssemblyName), but no referenced assembly with that name was found in the host compilation. Ensure the generated host project references the consumer library.", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                return;
            }

            foreach (INamedTypeSymbol type in ReferencedAssemblyTypeWalker.GetAllTypes(consumerAssembly))
            {
                // The generated host code lives in a separate assembly. It NAMES only two kinds of consumer
                // type directly across the assembly boundary: the IEcmaStartup class and the [*Configuration]
                // option classes. Those "host-referenced participants" must therefore be public. A non-public
                // participant is a misconfiguration the implementor must fix - report ECMA2013 with the type
                // name and do NOT feed it through discovery (emitting code that names an inaccessible type would
                // fail later with a cryptic CS0122).
                if (!IsExternallyAccessible(type) && receiver.IsHostReferencedParticipant(type, context.Compilation))
                {
                    if (receiver.ImplementsEcmaStartup(type, context.Compilation))
                    {
                        receiver.HasNonPublicStartupCandidate = true;
                    }

                    Location location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2013", "A framework participant type must be public", $"The type '{type.ToDisplayString()}' implements IEcmaStartup or carries a [*Configuration] attribute, but it is not public. In this model the consumer is a library and the generated host executable is a separate assembly that can only reference the consumer's public types. Make this type public.", "Ecma2Framework", DiagnosticSeverity.Error, true), location));
                    continue;
                }

                // Every other type - including INTERNAL provider implementations - is fed through discovery.
                // Providers are never named by the generated code (the consumer registers them in its own
                // SetupServices and the host resolves them via DI), so they may stay internal; but the
                // worker-role existence checks (ECMA2002-2005) must still SEE them, so they are discovered here.
                receiver.ProcessType(type, context.Compilation);
            }
        }

        /// <summary>
        /// Returns true only when the type is accessible from a SEPARATE assembly - i.e. the type itself and every
        /// containing type are public. A public type nested inside a non-public outer type is NOT externally
        /// accessible, so the generated host assembly cannot name it; treating it as accessible would emit code that
        /// fails at host compile with CS0122. Used to decide ECMA2013 on the effective accessibility, not the type's
        /// own modifier alone.
        /// </summary>
        private static bool IsExternallyAccessible(INamedTypeSymbol type)
        {
            INamedTypeSymbol current = type;
            while (current != null)
            {
                if (current.DeclaredAccessibility != Accessibility.Public)
                {
                    return false;
                }

                current = current.ContainingType;
            }

            return true;
        }

        /// <summary>
        /// Executes the worker role. This path runs the full consumer discovery and reports the discovery
        /// diagnostics (ECMA2001-2005, plus any the syntax receiver raised), requires the management agent name
        /// (ECMA2011), and emits the worker entry point and config providers only when no errors were found.
        /// </summary>
        private void ExecuteWorkerRole(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            bool hasErrors = false;

            if (!receiver.HasStartupClass || string.IsNullOrWhiteSpace(receiver.StartupClassName))
            {
                // A non-public startup was already discovered and flagged with the more specific ECMA2013
                // ("make it public"). Suppress the contradictory ECMA2001 ("no startup found") so the implementor
                // gets one accurate diagnostic - but this is still an error state (there is no usable startup the
                // generated Main can name), so emission must be suppressed either way.
                if (!receiver.HasNonPublicStartupCandidate)
                {
                    context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2001", "Could not find the IEcmaStartup implementation", $"A startup class could not be found. Please generate a class that implements from IEcmaStartup", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                }

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

            // The effective management agent name is read in the worker role too, for the ECMA2011 check. It is
            // derived in MSBuild (consumer assembly name + ".Ecma2", or the optional override verbatim) and so is
            // effectively always present; ECMA2011 fires only if MSBuild resolved no name at all.
            if (!this.TryGetManagementAgentName(context, out _))
            {
                hasErrors = true;
            }

            if (hasErrors)
            {
                return;
            }

            this.EmitWorkerSources(context, receiver);
        }

        /// <summary>
        /// Reads the EFFECTIVE management agent name from the <c>Ecma2ManagementAgentName</c> MSBuild property.
        /// The effective name is computed in the framework targets - the consumer's assembly name plus the
        /// <c>.Ecma2</c> suffix by default, or the optional <c>Ecma2ManagementAgentName</c> override verbatim - and
        /// passed to the generator through this same property. It becomes the generated shim's assembly name and
        /// namespace (the MA's FIM identity). Because the targets always supply a derived value, this read
        /// effectively never fails; ECMA2011 is retained only as a last-resort guard for the near-impossible case
        /// that no effective name reached the generator at all, in which case emission is suppressed rather than
        /// producing an MA with no identity.
        /// </summary>
        private bool TryGetManagementAgentName(GeneratorExecutionContext context, out string managementAgentName)
        {
            if (!context.AnalyzerConfigOptions.GlobalOptions.TryGetValue(BuildPropertyManagementAgentName, out managementAgentName) || string.IsNullOrWhiteSpace(managementAgentName))
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2011", "The management agent name could not be resolved", "No effective management agent name reached the source generator. The framework targets derive it from the consumer project's assembly name (<assembly>.Ecma2), or from the optional <Ecma2ManagementAgentName> override. This value becomes the generated shim's assembly name (the management agent's FIM identity). Ensure the consumer project imports the Lithnet.Ecma2Framework targets and has a non-empty AssemblyName.", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                managementAgentName = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Emits the worker-role sources: the generated worker entry point (Main), the config-registration
        /// provider, and (when the consumer declares any [*Configuration] classes) the generated config-parameters
        /// provider. The consumer project compiles to the worker executable, so the generated Main becomes its
        /// entry point and calls into the WorkerEntryPoint runtime in the referenced Worker assembly.
        /// </summary>
        private void EmitWorkerSources(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            this.AddWorkerProgram(context, receiver);
            this.AddConfigParameterMapping(context, receiver);
            this.AddGeneratedConfigParametersSource(context, receiver);
        }

        private void AddWorkerProgram(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            string workerProgramText = this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.WorkerProgram.txt").Replace("%STARTUPCLASSNAME%", receiver.StartupClassName);
            context.AddSource("WorkerProgram.g.cs", SourceText.From(workerProgramText, Encoding.UTF8));
        }

        private void AddConfigParameterMapping(GeneratorExecutionContext context, Ecma2InitializerSyntaxReceiver receiver)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var mapping in receiver.MmsNameToKeyMapping)
            {
                builder.AppendLine($"{{ {ToLiteral(mapping.Key)}, {ToLiteral(mapping.Value)}}},");
            }

            var configMappingText = this.GetResource("Lithnet.Ecma2Framework.SourceGeneration.Templates.Ecma2ConfigRegistrationProvider.txt").Replace("%MAPPEDPROPERTIES%", builder.ToString());

            builder.Clear();

            foreach (var mapping in receiver.MmsNameToTypeMapping)
            {
                builder.AppendLine($"{{ {ToLiteral(mapping.Key)}, {ToLiteral(mapping.Value)}}},");
            }

            configMappingText = configMappingText.Replace("%TYPEMAPPINGS%", builder.ToString());

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

            context.AddSource("Ecma2ConfigRegistrationProvider.g.cs", SourceText.From(configMappingText, Encoding.UTF8));
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

        private static string ToLiteral(string value)
        {
            return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value ?? string.Empty, true);
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
