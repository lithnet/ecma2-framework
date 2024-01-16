using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lithnet.Ecma2Framework
{
    internal class Ecma2InitializerSyntaxReceiver : ISyntaxContextReceiver
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

                if (this.HasInterface(declaredSymbol, context, "Lithnet.Ecma2Framework.IEcmaBootstrapper"))
                {
                    this.HasBootstrapper = true;
                    this.BootstrapperClassName = fullyQualifiedName;
                }
                else if (this.HasInterface(declaredSymbol, context, "Lithnet.Ecma2Framework.ICapabilitiesProvider"))
                {
                    this.HasCapabilityProvider = true;
                }
                else if (this.HasInterface(declaredSymbol, context, "Lithnet.Ecma2Framework.IConfigParametersProvider"))
                {
                    this.HasConfigProvider = true;
                }
                else if (this.HasInterface(declaredSymbol, context, "Lithnet.Ecma2Framework.IObjectExportProvider"))
                {
                    this.HasObjectExportProvider = true;
                }
                else if (this.HasInterface(declaredSymbol, context, "Lithnet.Ecma2Framework.IObjectImportProvider"))
                {
                    this.HasObjectImportProvider = true;
                }
                else if (this.HasInterface(declaredSymbol, context, "Lithnet.Ecma2Framework.ISchemaProvider"))
                {
                    this.HasSchemaProvider = true;
                }
            }
        }

        /// <summary>Indicates whether or not the class has a specific interface.</summary>
        /// <returns>Whether or not the SyntaxList contains the attribute.</returns>
        private bool HasInterface(INamedTypeSymbol declaredTypeSymbol, GeneratorSyntaxContext context, string interfaceName)
        {
            var namedTypeSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(interfaceName);

            foreach (var interfaceTypeSymbol in declaredTypeSymbol.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(interfaceTypeSymbol, namedTypeSymbol))
                {
                    return true;
                }
            }

            return false;
        }
    }
}