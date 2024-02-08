using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Lithnet.Ecma2Framework
{
    internal class Ecma2InitializerSyntaxReceiver : ISyntaxContextReceiver
    {
        private const string AttributeAnnotationsDefaultValue = "System.ComponentModel.DefaultValueAttribute";

        private const string AttributeConfigPageConnectivity = "Lithnet.Ecma2Framework.ConnectivityConfigurationAttribute";
        private const string AttributeConfigPageGlobal = "Lithnet.Ecma2Framework.GlobalConfigurationAttribute";
        private const string AttributeConfigPageCapability = "Lithnet.Ecma2Framework.CapabilitiesConfigurationAttribute";
        private const string AttributeConfigPagePartition = "Lithnet.Ecma2Framework.PartitionConfigurationAttribute";
        private const string AttributeConfigPageRunStep = "Lithnet.Ecma2Framework.RunStepConfigurationAttribute";
        private const string AttributeConfigPageSchema = "Lithnet.Ecma2Framework.SchemaConfigurationAttribute";

        private const string InterfaceIEcmaStartup = "Lithnet.Ecma2Framework.IEcmaStartup";
        private const string InterfaceICapabilitiesProvider = "Lithnet.Ecma2Framework.ICapabilitiesProvider";
        private const string InterfaceIConfigParametersProvider = "Lithnet.Ecma2Framework.IConfigParametersProvider";
        private const string InterfaceIObjectExportProvider = "Lithnet.Ecma2Framework.IObjectExportProvider";
        private const string InterfaceIObjectImportProvider = "Lithnet.Ecma2Framework.IObjectImportProvider";
        private const string InterfaceISchemaProvider = "Lithnet.Ecma2Framework.ISchemaProvider";

        private const string AttributeConfigParamString = "Lithnet.Ecma2Framework.StringParameterAttribute";
        private const string AttributeConfigParamEncryptedString = "Lithnet.Ecma2Framework.EncryptedStringParameterAttribute";
        private const string AttributeConfigParamCheckbox = "Lithnet.Ecma2Framework.CheckboxParameterAttribute";
        private const string AttributeConfigParamDropdown = "Lithnet.Ecma2Framework.DropdownParameterAttribute";
        private const string AttributeConfigParamMultilineTextBox = "Lithnet.Ecma2Framework.MultilineTextboxParameterAttribute";
        private const string AttributeConfigParamFile = "Lithnet.Ecma2Framework.FileParameterAttribute";
        private const string AttributeConfigParamLabel = "Lithnet.Ecma2Framework.LabelParameterAttribute";
        private const string AttributeConfigParamDivider = "Lithnet.Ecma2Framework.DividerParameterAttribute";

        private const string ConfigParamDataTypeString = "string";
        private const string ConfigParamDataTypeStringEncrypted = "string-encrypted";
        private const string ConfigParamDataTypeBoolean = "boolean";
        private const string ConfigParamDataTypeStringDropdown = "string-dropdown";
        private const string ConfigParamDataTypeStringMultiline = "string-multiline";
        private const string ConfigParamDataTypeStringFile = "string-file";

        private static readonly SymbolDisplayFormat fullTypeNameFormat = new SymbolDisplayFormat(typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        public Dictionary<string, string> MmsNameToKeyMapping { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> MmsNameToTypeMapping { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public List<string> ServicesToRegister { get; } = new List<string>();

        public List<DiscoveredConfigClass> DiscoveredConfigClasses { get; } = new List<DiscoveredConfigClass>();

        public List<Diagnostic> Diagnostics { get; } = new List<Diagnostic>();

        public string BootstrapperClassName { get; set; }

        public bool HasBootstrapper { get; set; }

        public bool HasCapabilityProvider { get; set; }

        public bool HasConfigProvider { get; set; }

        public bool HasConfigAttributes { get; set; }

        public bool HasSchemaProvider { get; set; }

        public bool HasObjectImportProvider { get; set; }

        public bool HasObjectExportProvider { get; set; }

        /// <summary>
        /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
        /// </summary>
        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            try
            {
                if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
                {
                    INamedTypeSymbol declaredSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) as INamedTypeSymbol;

                    if (declaredSymbol == null || declaredSymbol.IsStatic || declaredSymbol.IsAbstract)
                    {
                        return;
                    }

                    string fullyQualifiedName = declaredSymbol.ToDisplayString(fullTypeNameFormat);

                    if (this.HasInterface(declaredSymbol, context, InterfaceIEcmaStartup))
                    {
                        this.HasBootstrapper = true;
                        this.BootstrapperClassName = fullyQualifiedName;
                    }
                    else if (this.HasInterface(declaredSymbol, context, InterfaceICapabilitiesProvider))
                    {
                        this.HasCapabilityProvider = true;
                    }
                    else if (this.HasInterface(declaredSymbol, context, InterfaceIConfigParametersProvider))
                    {
                        this.HasConfigProvider = true;
                    }
                    else if (this.HasInterface(declaredSymbol, context, InterfaceIObjectExportProvider))
                    {
                        this.HasObjectExportProvider = true;
                    }
                    else if (this.HasInterface(declaredSymbol, context, InterfaceIObjectImportProvider))
                    {
                        this.HasObjectImportProvider = true;
                    }
                    else if (this.HasInterface(declaredSymbol, context, InterfaceISchemaProvider))
                    {
                        this.HasSchemaProvider = true;
                    }

                    foreach (var attribute in declaredSymbol.GetAttributes())
                    {
                        string attributeName = attribute.AttributeClass.ToDisplayString(fullTypeNameFormat);
                        DiscoveredConfigClass configClass = new DiscoveredConfigClass();

                        switch (attributeName)
                        {
                            case AttributeConfigPageConnectivity:
                                configClass.Page = ConfigParameterPage.Connectivity;
                                break;

                            case AttributeConfigPageCapability:
                                configClass.Page = ConfigParameterPage.Capabilities;
                                break;

                            case AttributeConfigPageGlobal:
                                configClass.Page = ConfigParameterPage.Global;
                                break;

                            case AttributeConfigPagePartition:
                                configClass.Page = ConfigParameterPage.Partition;
                                break;

                            case AttributeConfigPageRunStep:
                                configClass.Page = ConfigParameterPage.RunStep;
                                break;

                            case AttributeConfigPageSchema:
                                configClass.Page = ConfigParameterPage.Schema;
                                break;

                            default:
                                continue;
                        }

                        configClass.ClassSymbol = declaredSymbol;
                        configClass.ConfigAttribute = attribute;
                        configClass.SectionName = attribute.ConstructorArguments[0].Value?.ToString();
                        configClass.ClassName = declaredSymbol.ToDisplayString(fullTypeNameFormat);
                        if (string.IsNullOrWhiteSpace(configClass.SectionName))
                        {
                            continue;
                        }

                        this.VisitConfigClassAttribute(configClass);
                        this.DiscoveredConfigClasses.Add(configClass);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Diagnostics.Add(Diagnostic.Create(new DiagnosticDescriptor("ECMA2000", "An unexpected error occurred", $"An unexpected error occurred: {ex}", "Ecma2Framework", DiagnosticSeverity.Error, true), Location.None));
            }
        }

        private void VisitConfigClassAttribute(DiscoveredConfigClass configClass)
        {
            this.HasConfigAttributes = true;

            configClass.ServicesToRegister.Add($"services.Configure<{configClass.ClassName}>(configuration.GetSection(\"{configClass.SectionName}\"));");

            foreach (var property in configClass.ClassSymbol.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.GetAttributes().Any(t => t.AttributeClass.ToDisplayString(fullTypeNameFormat) == AttributeConfigParamDivider))
                {
                    configClass.ParametersToAdd.Add(this.CreateDividerParameterEntry());
                }

                var labelAttribute = property.GetAttributes().FirstOrDefault(t => t.AttributeClass.ToDisplayString(fullTypeNameFormat) == AttributeConfigParamLabel);
                if (labelAttribute != null)
                {
                    var mmsName = labelAttribute.ConstructorArguments[0].Value?.ToString();
                    configClass.ParametersToAdd.Add(this.CreateLabelParameterEntry(mmsName));
                }

                foreach (var propertyAttribute in property.GetAttributes())
                {
                    this.VisitPropertyAttribute(configClass, property, propertyAttribute);
                }
            }
        }

        private void VisitPropertyAttribute(DiscoveredConfigClass configClass, IPropertySymbol property, AttributeData propertyAttribute)
        {
            string propertyAttributeName = propertyAttribute.AttributeClass.ToDisplayString(fullTypeNameFormat);

            if (propertyAttributeName != AttributeConfigParamString &&
                propertyAttributeName != AttributeConfigParamCheckbox &&
                propertyAttributeName != AttributeConfigParamDropdown &&
                propertyAttributeName != AttributeConfigParamEncryptedString &&
                propertyAttributeName != AttributeConfigParamMultilineTextBox &&
                propertyAttributeName != AttributeConfigParamFile)
            {
                return;
            }

            if (!configClass.PropertiesDecorated.Add(property.Name))
            {
                this.Diagnostics.Add(Diagnostic.Create(new DiagnosticDescriptor("ECMA2006", "Multiple parameter type declarations are not permitted on the same property", $"The attribute [{propertyAttribute.AttributeClass.Name}] cannot be applied to the property '{property.Name}' because it is already defined by another configuration parameter", "Ecma2Framework", DiagnosticSeverity.Error, true), property.Locations[0]));
                return;
            }

            var mmsName = propertyAttribute.ConstructorArguments[0].Value?.ToString();

            if (!string.IsNullOrWhiteSpace(mmsName))
            {
                if (this.MmsNameToKeyMapping.ContainsKey(mmsName))
                {
                    this.Diagnostics.Add(Diagnostic.Create(new DiagnosticDescriptor("ECMA2007", "Configuration parameter names must be unique", $"The configuration parameter name '{mmsName}' is already in use on another property", "Ecma2Framework", DiagnosticSeverity.Error, true), property.Locations[0]));
                    return;
                }

                this.MmsNameToKeyMapping.Add(mmsName, $"{configClass.SectionName}:{property.Name}");

                this.MmsNameToTypeMapping.Add(mmsName, propertyAttributeName switch
                {
                    AttributeConfigParamString => ConfigParamDataTypeString,
                    AttributeConfigParamEncryptedString => ConfigParamDataTypeStringEncrypted,
                    AttributeConfigParamCheckbox => ConfigParamDataTypeBoolean,
                    AttributeConfigParamDropdown => ConfigParamDataTypeStringDropdown,
                    AttributeConfigParamMultilineTextBox => ConfigParamDataTypeStringMultiline,
                    AttributeConfigParamFile => ConfigParamDataTypeStringFile,
                    _ => null,
                });
            }

            var configDefinitionEntry = propertyAttributeName switch
            {
                AttributeConfigParamString => this.CreateStringParameterEntry(property, mmsName),
                AttributeConfigParamEncryptedString => this.CreateEncryptedStringParameterEntry(property, mmsName),
                AttributeConfigParamCheckbox => this.CreateCheckboxParameterEntry(property, mmsName),
                AttributeConfigParamDropdown => this.CreateDropdownParameterEntry(property, propertyAttribute, mmsName),
                AttributeConfigParamMultilineTextBox => this.CreateMultilineTextParameterEntry(property, mmsName),
                AttributeConfigParamFile => this.CreateFileParameterEntry(property, mmsName),
                _ => null,
            };

            if (configDefinitionEntry != null)
            {
                configClass.ParametersToAdd.Add(configDefinitionEntry);
            }
        }

        private string CreateDividerParameterEntry()
        {
            return "newDefinitions.Add(ConfigParameterDefinition.CreateDividerParameter());";
        }

        private string CreateLabelParameterEntry(string name)
        {
            return $"newDefinitions.Add(ConfigParameterDefinition.CreateLabelParameter(\"{name}\"));";
        }

        private string CreateDropdownParameterEntry(IPropertySymbol property, AttributeData attribute, string name)
        {
            string defaultValue = GetDefaultValue<string>(property, null);
            defaultValue = defaultValue == null ? "string.Empty" : $"\"{defaultValue}\"";

            string extensible = (attribute.ConstructorArguments[1].Value as bool? ?? false).ToString().ToLowerInvariant();
            string items = $"new string [] {{{string.Join(", ", (attribute.ConstructorArguments[2].Values.Select(u => u.Value)).Select(t => $"\"{t}\""))}}}";

            return $"newDefinitions.Add(ConfigParameterDefinition.CreateDropDownParameter(\"{name}\", {items}, {extensible}, {defaultValue}));";
        }

        private string CreateFileParameterEntry(IPropertySymbol property, string name)
        {
            string defaultValue = GetDefaultValue<string>(property, null);
            defaultValue = defaultValue == null ? "string.Empty" : $"\"{defaultValue}\"";

            return $"newDefinitions.Add(ConfigParameterDefinition.CreateFileParameter(\"{name}\", {defaultValue}));";
        }

        private string CreateCheckboxParameterEntry(IPropertySymbol property, string name)
        {
            string defaultValue = GetDefaultValue(property, false).ToString().ToLowerInvariant();
            return $"newDefinitions.Add(ConfigParameterDefinition.CreateCheckBoxParameter(\"{name}\", {defaultValue}));";
        }

        private string CreateMultilineTextParameterEntry(IPropertySymbol property, string name)
        {
            string defaultValue = GetDefaultValue<string>(property, null);
            defaultValue = defaultValue == null ? "string.Empty" : $"\"{defaultValue}\"";

            return $"newDefinitions.Add(ConfigParameterDefinition.CreateTextParameter(\"{name}\", {defaultValue}));";
        }

        private string CreateStringParameterEntry(IPropertySymbol property, string name)
        {
            string defaultValue = GetDefaultValue<string>(property, null);
            defaultValue = defaultValue == null ? "string.Empty" : $"\"{defaultValue}\"";

            return $"newDefinitions.Add(ConfigParameterDefinition.CreateStringParameter(\"{name}\", string.Empty, {defaultValue}));";
        }

        private string CreateEncryptedStringParameterEntry(IPropertySymbol property, string name)
        {
            string defaultValue = GetDefaultValue<string>(property, null);
            defaultValue = defaultValue == null ? "string.Empty" : $"\"{defaultValue}\"";
            return $"newDefinitions.Add(ConfigParameterDefinition.CreateEncryptedStringParameter(\"{name}\", string.Empty, {defaultValue}));";
        }

        private static T GetDefaultValue<T>(IPropertySymbol property, T defaultValue)
        {
            var defaultValueProperty = property.GetAttributes().FirstOrDefault(t => t.AttributeClass.ToDisplayString(fullTypeNameFormat) == AttributeAnnotationsDefaultValue);

            if (defaultValueProperty == null)
            {
                return defaultValue;
            }

            var v = defaultValueProperty.ConstructorArguments[0].Value?.ToString();
            if (v != null)
            {
                if (typeof(T) == typeof(bool))
                {
                    if (bool.TryParse(v, out bool b))
                    {
                        return (T)(object)b;
                    }
                }
                else if (typeof(T) == typeof(int))
                {
                    if (int.TryParse(v, out int i))
                    {
                        return (T)(object)i;
                    }
                }
                else if (typeof(T) == typeof(string))
                {
                    return (T)(object)v;
                }
            }

            return defaultValue;
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