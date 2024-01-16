using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Lithnet.Ecma2Framework
{
    [Generator]
    public class EcmaGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new IEcma2InitializerSyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxContextReceiver is not IEcma2InitializerSyntaxReceiver receiver)
            {
                return;
            }

            bool hasErrors = false;

            if (!receiver.HasBootstrapper || string.IsNullOrWhiteSpace(receiver.BootstrapperClassName))
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2001", "Could not find the bootstrapper implementation", $"A bootstrapper class could not be found. Please generate a class that implements from {typeof(IEcmaBootstrapper).FullName}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (!receiver.HasSchemaProvider)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2002", "Could not find the schema provider implementation", $"A schema provider class could not be found. Please generate a class that implements from {typeof(ISchemaProvider).FullName}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (!receiver.HasCapabilityProvider)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2003", "Could not find the capabilities provider implementation", $"A capabilities provider class could not be found. Please generate a class that implements from {typeof(ICapabilitiesProvider).FullName}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (!receiver.HasObjectExportProvider && !receiver.HasObjectImportProvider)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor("ECMA2004", "Could not find the object import or export provider implementation", $"An object provider class could not be found. Please generate at least one class that implements from {typeof(IObjectExportProvider).FullName} or {typeof(IObjectImportProvider).FullName}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
                hasErrors = true;
            }

            if (hasErrors)
            {
                return;
            }

            context.AddSource("Ecma2Bootstrapper.g.cs", SourceText.From(@"
namespace Lithnet.Ecma2Framework
{
    internal static class Ecma2Bootstrapper
    {
        private static Ecma2Initializer initializer;

        public static Ecma2Initializer GetInitializer()
        {
            if (initializer == null)
            {
                var bootStrapper = new " + receiver.BootstrapperClassName + @"();
                initializer = new Ecma2Initializer(bootStrapper);
            }

            return initializer;
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2ImportImplementation.g.cs", SourceText.From(@"
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2ImportImplementation : IMAExtensible2CallImport
    {
        private Ecma2Import provider;

        public Ecma2ImportImplementation()
        {
            var initializer = Ecma2Bootstrapper.GetInitializer();
            this.provider = new Ecma2Import(initializer);
        }

        int IMAExtensible2CallImport.ImportDefaultPageSize => 100;

        int IMAExtensible2CallImport.ImportMaxPageSize => 9999;

        OpenImportConnectionResults IMAExtensible2CallImport.OpenImportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            return AsyncHelper.RunSync(this.provider.OpenImportConnectionAsync(configParameters, types, importRunStep));
        }
        GetImportEntriesResults IMAExtensible2CallImport.GetImportEntries(GetImportEntriesRunStep importRunStep)
        {
            return AsyncHelper.RunSync(this.provider.GetImportEntriesPageAsync());
        }

        CloseImportConnectionResults IMAExtensible2CallImport.CloseImportConnection(CloseImportConnectionRunStep importRunStep)
        {
            return AsyncHelper.RunSync(this.provider.CloseImportConnectionAsync(importRunStep));
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2ExportImplementation.g.cs", SourceText.From(@"
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2ExportImplementation : IMAExtensible2CallExport
    {
        private Ecma2Export provider;

        public Ecma2ExportImplementation()
        {
            var initializer = Ecma2Bootstrapper.GetInitializer();
            this.provider = new Ecma2Export(initializer);
        }

        int IMAExtensible2CallExport.ExportDefaultPageSize => 100;

        int IMAExtensible2CallExport.ExportMaxPageSize => 9999;

        void IMAExtensible2CallExport.OpenExportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            AsyncHelper.RunSync(this.provider.OpenExportConnectionAsync(configParameters, types, exportRunStep));
        }

        PutExportEntriesResults IMAExtensible2CallExport.PutExportEntries(IList<CSEntryChange> csentries)
        {
            return AsyncHelper.RunSync(this.provider.PutExportEntriesAsync(csentries));
        }

        void IMAExtensible2CallExport.CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
            AsyncHelper.RunSync(this.provider.CloseExportConnectionAsync(exportRunStep));
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2PasswordImplementation.g.cs", SourceText.From(@"
using System.Collections.ObjectModel;
using System.Security;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2PasswordImplementation : IMAExtensible2Password
    {
        private Ecma2Password provider;

        public Ecma2PasswordImplementation()
        {
            var initializer = Ecma2Bootstrapper.GetInitializer();
            this.provider = new Ecma2Password(initializer);
        }

        void IMAExtensible2Password.OpenPasswordConnection(KeyedCollection<string, ConfigParameter> configParameters, Partition partition)
        {
            AsyncHelper.RunSync(this.provider.OpenPasswordConnectionAsync(configParameters, partition));
        }

        void IMAExtensible2Password.ClosePasswordConnection()
        {
            AsyncHelper.RunSync(this.provider.ClosePasswordConnectionAsync());
        }

        ConnectionSecurityLevel IMAExtensible2Password.GetConnectionSecurityLevel()
        {
            return AsyncHelper.RunSync(this.provider.GetConnectionSecurityLevelAsync());
        }

        void IMAExtensible2Password.SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            AsyncHelper.RunSync(this.provider.SetPasswordAsync(csentry, newPassword, options));
        }

        void IMAExtensible2Password.ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            AsyncHelper.RunSync(this.provider.ChangePasswordAsync(csentry, oldPassword, newPassword));
        }
    }
}
", Encoding.UTF8));

            context.AddSource("Ecma2Implementation.g.cs", SourceText.From(@"
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class Ecma2Implementation :
        IMAExtensible2GetSchema,
        IMAExtensible2GetCapabilitiesEx,
        IMAExtensible2GetParametersEx,
        IMAExtensible2GetParameters
    {
        private Ecma2 provider;

        public Ecma2Implementation()
        {
            var initializer = Ecma2Bootstrapper.GetInitializer();
            this.provider = new Ecma2(initializer);
        }


        MACapabilities IMAExtensible2GetCapabilitiesEx.GetCapabilitiesEx(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return AsyncHelper.RunSync(this.provider.GetCapabilitiesAsync(configParameters));
        }

        IList<ConfigParameterDefinition> IMAExtensible2GetParametersEx.GetConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return AsyncHelper.RunSync(this.provider.GetConfigParametersAsync(configParameters, page, pageNumber));
        }

        ParameterValidationResult IMAExtensible2GetParametersEx.ValidateConfigParametersEx(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page, int pageNumber)
        {
            return AsyncHelper.RunSync(this.provider.ValidateConfigParametersAsync(configParameters, page, pageNumber));
        }

        Schema IMAExtensible2GetSchema.GetSchema(KeyedCollection<string, ConfigParameter> configParameters)
        {
            return AsyncHelper.RunSync(this.provider.GetSchemaAsync(configParameters));
        }

        IList<ConfigParameterDefinition> IMAExtensible2GetParameters.GetConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return AsyncHelper.RunSync(this.provider.GetConfigParametersAsync(configParameters, page, 1));
        }

        ParameterValidationResult IMAExtensible2GetParameters.ValidateConfigParameters(KeyedCollection<string, ConfigParameter> configParameters, ConfigParameterPage page)
        {
            return AsyncHelper.RunSync(this.provider.ValidateConfigParametersAsync(configParameters, page, 1));
        }
    }
}
", Encoding.UTF8));
        }


        internal class IEcma2InitializerSyntaxReceiver : ISyntaxContextReceiver
        {
            public string BootstrapperClassName { get; set; }

            public bool HasBootstrapper { get; set; }

            public bool HasCapabilityProvider { get; set; }

            public bool HasConfigProvider { get; set; }

            public bool HasSchemaProvider { get; set; }

            public bool HasObjectImportProvider { get; set; }

            public bool HasObjectExportProvider { get; set; }


            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                // any field with at least one attribute is a candidate for property generation
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    INamedTypeSymbol declaredSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;

                    if (declaredSymbol == null || declaredSymbol.IsStatic || declaredSymbol.IsAbstract)
                    {
                        return;
                    }

                    var symbolDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
                    string fullyQualifiedName = declaredSymbol.ToDisplayString(symbolDisplayFormat);

                    if (declaredSymbol.HasInterface(context, typeof(IEcmaBootstrapper).FullName))
                    {
                        this.HasBootstrapper = true;
                        this.BootstrapperClassName = fullyQualifiedName;
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(ICapabilitiesProvider).FullName))
                    {
                        this.HasCapabilityProvider = true;
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IConfigParametersProvider).FullName))
                    {
                        this.HasConfigProvider = true;
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IObjectExportProvider).FullName))
                    {
                        this.HasObjectExportProvider = true;
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IObjectImportProvider).FullName))
                    {
                        this.HasObjectImportProvider = true;
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(ISchemaProvider).FullName))
                    {
                        this.HasSchemaProvider = true;
                    }
                }
            }
        }

        internal class InterfaceImplementationSyntaxReceiver : ISyntaxContextReceiver
        {
            public Dictionary<string, List<string>> ClassesToAdd { get; } = new Dictionary<string, List<string>>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
            {
                // any field with at least one attribute is a candidate for property generation
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    INamedTypeSymbol declaredSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;

                    if (declaredSymbol == null || declaredSymbol.IsStatic || declaredSymbol.IsAbstract)
                    {
                        return;
                    }

                    var symbolDisplayFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);
                    string fullyQualifiedName = declaredSymbol.ToDisplayString(symbolDisplayFormat);

                    if (declaredSymbol.HasInterface(context, typeof(ICapabilitiesProvider).FullName))
                    {
                        this.AddTypeToList(typeof(ICapabilitiesProvider).FullName, fullyQualifiedName);
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IConfigParametersProvider).FullName))
                    {
                        this.AddTypeToList(typeof(IConfigParametersProvider).FullName, fullyQualifiedName);
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IObjectExportProvider).FullName))
                    {
                        this.AddTypeToList(typeof(IObjectExportProvider).FullName, fullyQualifiedName);
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IObjectImportProvider).FullName))
                    {
                        this.AddTypeToList(typeof(IObjectImportProvider).FullName, fullyQualifiedName);
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IObjectPasswordProvider).FullName))
                    {
                        this.AddTypeToList(typeof(IObjectPasswordProvider).FullName, fullyQualifiedName);
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(ISchemaProvider).FullName))
                    {
                        this.AddTypeToList(typeof(ISchemaProvider).FullName, fullyQualifiedName);
                    }
                    else if (declaredSymbol.HasInterface(context, typeof(IOperationInitializer).FullName))
                    {
                        this.AddTypeToList(typeof(IOperationInitializer).FullName, fullyQualifiedName);
                    }
                }
            }

            private void AddTypeToList(string interfaceName, string concreteType)
            {
                if (!this.ClassesToAdd.TryGetValue(interfaceName, out var list))
                {
                    list = new List<string>();
                    list.Add(concreteType);
                    this.ClassesToAdd.Add(interfaceName, list);
                }
                else
                {
                    list.Add(concreteType);
                }
            }
        }
    }
}
