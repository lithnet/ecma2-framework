using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;
using static Lithnet.Ecma2Framework.Serialization.MmsSerializationSurrogateExtensions;

namespace Lithnet.Ecma2Framework.Serialization
{
#if NETFRAMEWORK
    // net4x (shim) surrogate. Ported verbatim from ACMA's MmsSerializationSurrogate
    // (Lithnet.MetadirectoryServices). Implements the legacy IDataContractSurrogate, including
    // the six WCF-codegen methods that exist on net4x but were removed in net8.
#if ECMA2_SHIM_INTERNAL
    internal class MmsSerializationSurrogate : IDataContractSurrogate
#else
    public class MmsSerializationSurrogate : IDataContractSurrogate
#endif
    {
        public object GetCustomDataToExport(Type clrType, Type dataContractType)
        {
            return null;
        }

        public object GetCustomDataToExport(System.Reflection.MemberInfo memberInfo, Type dataContractType)
        {
            return null;
        }

        public Type GetDataContractType(Type type)
        {
            if (typeof(CSEntryChange).IsAssignableFrom(type))
            {
                return typeof(CSEntryChangeSerializable);
            }
            else if (typeof(AttributeChange).IsAssignableFrom(type))
            {
                return typeof(AttributeChangeSerializable);
            }
            else if (typeof(ValueChange).IsAssignableFrom(type))
            {
                return typeof(ValueChangeSerializable);
            }
            else if (typeof(AnchorAttribute).IsAssignableFrom(type))
            {
                return typeof(AnchorAttributeSerializable);
            }
            else if (typeof(SchemaAttribute).IsAssignableFrom(type))
            {
                return typeof(SchemaAttributeSerializable);
            }
            else if (typeof(SchemaType).IsAssignableFrom(type))
            {
                return typeof(SchemaTypeSerializable);
            }
            else if (typeof(Schema).IsAssignableFrom(type))
            {
                return typeof(SchemaSerializable);
            }
            else if (typeof(CSEntryChangeResult).IsAssignableFrom(type))
            {
                return typeof(CSEntryChangeResultSerializable);
            }
            else if (typeof(MACapabilities).IsAssignableFrom(type))
            {
                return typeof(MACapabilitiesSerializable);
            }
            else if (typeof(ConfigParameter).IsAssignableFrom(type))
            {
                return typeof(ConfigParameterSerializable);
            }
            else if (typeof(ConfigParameterDefinition).IsAssignableFrom(type))
            {
                return typeof(ConfigParameterDefinitionSerializable);
            }
            else if (typeof(ParameterValidationResult).IsAssignableFrom(type))
            {
                return typeof(ParameterValidationResultSerializable);
            }
            else if (typeof(OpenImportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(OpenImportConnectionRunStepSerializable);
            }
            else if (typeof(OpenExportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(OpenExportConnectionRunStepSerializable);
            }
            else if (typeof(CloseImportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(CloseImportConnectionRunStepSerializable);
            }
            else if (typeof(CloseExportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(CloseExportConnectionRunStepSerializable);
            }
            else if (typeof(GetImportEntriesRunStep).IsAssignableFrom(type))
            {
                return typeof(GetImportEntriesRunStepSerializable);
            }
            else if (typeof(Partition).IsAssignableFrom(type))
            {
                return typeof(PartitionSerializable);
            }
            else if (typeof(HierarchyNode).IsAssignableFrom(type))
            {
                return typeof(HierarchyNodeSerializable);
            }
            else if (typeof(ChangeTypeDescription).IsAssignableFrom(type))
            {
                return typeof(ChangeTypeDescriptionSerializable);
            }
            else if (typeof(CSEntryIdentity).IsAssignableFrom(type))
            {
                return typeof(CSEntryIdentitySerializable);
            }
            else if (typeof(CSEntryIdentityAttribute).IsAssignableFrom(type))
            {
                return typeof(CSEntryIdentityAttributeSerializable);
            }

            return type;
        }

        public object GetDeserializedObject(object obj, Type targetType)
        {
            ValueChangeSerializable valueChange = obj as ValueChangeSerializable;

            if (valueChange != null)
            {
                return valueChange.GetObject();
            }

            AttributeChangeSerializable attributeChange = obj as AttributeChangeSerializable;

            if (attributeChange != null)
            {
                return attributeChange.GetObject();
            }

            CSEntryChangeSerializable csentry = obj as CSEntryChangeSerializable;

            if (csentry != null)
            {
                return csentry.GetObject();
            }

            CSEntryChangeResultSerializable csentryresult = obj as CSEntryChangeResultSerializable;

            if (csentryresult != null)
            {
                return csentryresult.GetObject();
            }

            AnchorAttributeSerializable anchor = obj as AnchorAttributeSerializable;

            if (anchor != null)
            {
                return anchor.GetObject();
            }

            SchemaAttributeSerializable schemaAttribute = obj as SchemaAttributeSerializable;

            if (schemaAttribute != null)
            {
                return schemaAttribute.GetObject();
            }

            SchemaTypeSerializable schemaType = obj as SchemaTypeSerializable;

            if (schemaType != null)
            {
                return schemaType.GetObject();
            }

            SchemaSerializable schema = obj as SchemaSerializable;

            if (schema != null)
            {
                return schema.GetObject();
            }

            object rematerialised = GetDeserializedExtended(obj);

            if (rematerialised != null)
            {
                return rematerialised;
            }

            return obj;
        }

        public void GetKnownCustomDataTypes(System.Collections.ObjectModel.Collection<Type> customDataTypes)
        {
        }

        public object GetObjectToSerialize(object obj, Type targetType)
        {
            ValueChange valueChange = obj as ValueChange;

            if (valueChange != null)
            {
                return new ValueChangeSerializable(valueChange);
            }

            AttributeChange attributeChange = obj as AttributeChange;

            if (attributeChange != null)
            {
                return new AttributeChangeSerializable(attributeChange);
            }

            CSEntryChange csentry = obj as CSEntryChange;

            if (csentry != null)
            {
                return new CSEntryChangeSerializable(csentry);
            }

            CSEntryChangeResult csentryresult = obj as CSEntryChangeResult;

            if (csentryresult != null)
            {
                return new CSEntryChangeResultSerializable(csentryresult);
            }

            AnchorAttribute anchor = obj as AnchorAttribute;

            if (anchor != null)
            {
                return new AnchorAttributeSerializable(anchor);
            }

            SchemaAttribute schemaAttribute = obj as SchemaAttribute;

            if (schemaAttribute != null)
            {
                return new SchemaAttributeSerializable(schemaAttribute);
            }

            SchemaType schemaType = obj as SchemaType;

            if (schemaType != null)
            {
                return new SchemaTypeSerializable(schemaType);
            }

            Schema schema = obj as Schema;

            if (schema != null)
            {
                return new SchemaSerializable(schema);
            }

            object substitute = GetObjectToSerializeExtended(obj);

            if (substitute != null)
            {
                return substitute;
            }

            return obj;
        }

        public Type GetReferencedTypeOnImport(string typeName, string typeNamespace, object customData)
        {
            if (typeName.Equals("CSEntryChangeSerializable"))
            {
                return typeof(CSEntryChange);
            }
            else if (typeName.Equals("AttributeChangeSerializable"))
            {
                return typeof(AttributeChange);
            }
            else if (typeName.Equals("ValueChangeSerializable"))
            {
                return typeof(ValueChange);
            }
            else if (typeName.Equals("AnchorAttributeSerializable"))
            {
                return typeof(AnchorAttribute);
            }
            else if (typeName.Equals("SchemaAttributeSerializable"))
            {
                return typeof(SchemaAttribute);
            }
            else if (typeName.Equals("SchemaTypeSerializable"))
            {
                return typeof(SchemaType);
            }
            else if (typeName.Equals("SchemaSerializable"))
            {
                return typeof(Schema);
            }
            else if (typeName.Equals("CSEntryChangeResultSerializable"))
            {
                return typeof(CSEntryChangeResult);
            }
            else if (typeName.Equals("MACapabilitiesSerializable"))
            {
                return typeof(MACapabilities);
            }
            else if (typeName.Equals("ConfigParameterSerializable"))
            {
                return typeof(ConfigParameter);
            }
            else if (typeName.Equals("ConfigParameterDefinitionSerializable"))
            {
                return typeof(ConfigParameterDefinition);
            }
            else if (typeName.Equals("ParameterValidationResultSerializable"))
            {
                return typeof(ParameterValidationResult);
            }
            else if (typeName.Equals("OpenImportConnectionRunStepSerializable"))
            {
                return typeof(OpenImportConnectionRunStep);
            }
            else if (typeName.Equals("OpenExportConnectionRunStepSerializable"))
            {
                return typeof(OpenExportConnectionRunStep);
            }
            else if (typeName.Equals("CloseImportConnectionRunStepSerializable"))
            {
                return typeof(CloseImportConnectionRunStep);
            }
            else if (typeName.Equals("CloseExportConnectionRunStepSerializable"))
            {
                return typeof(CloseExportConnectionRunStep);
            }
            else if (typeName.Equals("GetImportEntriesRunStepSerializable"))
            {
                return typeof(GetImportEntriesRunStep);
            }
            else if (typeName.Equals("PartitionSerializable"))
            {
                return typeof(Partition);
            }
            else if (typeName.Equals("HierarchyNodeSerializable"))
            {
                return typeof(HierarchyNode);
            }
            else if (typeName.Equals("ChangeTypeDescriptionSerializable"))
            {
                return typeof(ChangeTypeDescription);
            }
            else if (typeName.Equals("CSEntryIdentitySerializable"))
            {
                return typeof(CSEntryIdentity);
            }
            else if (typeName.Equals("CSEntryIdentityAttributeSerializable"))
            {
                return typeof(CSEntryIdentityAttribute);
            }
            return null;
        }

        public System.CodeDom.CodeTypeDeclaration ProcessImportedType(System.CodeDom.CodeTypeDeclaration typeDeclaration, System.CodeDom.CodeCompileUnit compileUnit)
        {
            return typeDeclaration;
        }
    }
#else
    // net8 surrogate. net8 replaces the net4x IDataContractSurrogate with
    // ISerializationSurrogateProvider; the same substitution logic, ported from ACMA's
    // MmsSerializationSurrogate and extended to all eight DTO types. The six WCF-codegen
    // methods do not exist on this interface and are dropped.
#if ECMA2_SHIM_INTERNAL
    internal class MmsSerializationSurrogate : ISerializationSurrogateProvider
#else
    public class MmsSerializationSurrogate : ISerializationSurrogateProvider
#endif
    {
        public Type GetSurrogateType(Type type)
        {
            if (typeof(CSEntryChange).IsAssignableFrom(type))
            {
                return typeof(CSEntryChangeSerializable);
            }
            else if (typeof(AttributeChange).IsAssignableFrom(type))
            {
                return typeof(AttributeChangeSerializable);
            }
            else if (typeof(ValueChange).IsAssignableFrom(type))
            {
                return typeof(ValueChangeSerializable);
            }
            else if (typeof(AnchorAttribute).IsAssignableFrom(type))
            {
                return typeof(AnchorAttributeSerializable);
            }
            else if (typeof(SchemaAttribute).IsAssignableFrom(type))
            {
                return typeof(SchemaAttributeSerializable);
            }
            else if (typeof(SchemaType).IsAssignableFrom(type))
            {
                return typeof(SchemaTypeSerializable);
            }
            else if (typeof(Schema).IsAssignableFrom(type))
            {
                return typeof(SchemaSerializable);
            }
            else if (typeof(CSEntryChangeResult).IsAssignableFrom(type))
            {
                return typeof(CSEntryChangeResultSerializable);
            }
            else if (typeof(MACapabilities).IsAssignableFrom(type))
            {
                return typeof(MACapabilitiesSerializable);
            }
            else if (typeof(ConfigParameter).IsAssignableFrom(type))
            {
                return typeof(ConfigParameterSerializable);
            }
            else if (typeof(ConfigParameterDefinition).IsAssignableFrom(type))
            {
                return typeof(ConfigParameterDefinitionSerializable);
            }
            else if (typeof(ParameterValidationResult).IsAssignableFrom(type))
            {
                return typeof(ParameterValidationResultSerializable);
            }
            else if (typeof(OpenImportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(OpenImportConnectionRunStepSerializable);
            }
            else if (typeof(OpenExportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(OpenExportConnectionRunStepSerializable);
            }
            else if (typeof(CloseImportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(CloseImportConnectionRunStepSerializable);
            }
            else if (typeof(CloseExportConnectionRunStep).IsAssignableFrom(type))
            {
                return typeof(CloseExportConnectionRunStepSerializable);
            }
            else if (typeof(GetImportEntriesRunStep).IsAssignableFrom(type))
            {
                return typeof(GetImportEntriesRunStepSerializable);
            }
            else if (typeof(Partition).IsAssignableFrom(type))
            {
                return typeof(PartitionSerializable);
            }
            else if (typeof(HierarchyNode).IsAssignableFrom(type))
            {
                return typeof(HierarchyNodeSerializable);
            }
            else if (typeof(ChangeTypeDescription).IsAssignableFrom(type))
            {
                return typeof(ChangeTypeDescriptionSerializable);
            }
            else if (typeof(CSEntryIdentity).IsAssignableFrom(type))
            {
                return typeof(CSEntryIdentitySerializable);
            }
            else if (typeof(CSEntryIdentityAttribute).IsAssignableFrom(type))
            {
                return typeof(CSEntryIdentityAttributeSerializable);
            }

            return type;
        }

        public object GetObjectToSerialize(object obj, Type targetType)
        {
            ValueChange valueChange = obj as ValueChange;

            if (valueChange != null)
            {
                return new ValueChangeSerializable(valueChange);
            }

            AttributeChange attributeChange = obj as AttributeChange;

            if (attributeChange != null)
            {
                return new AttributeChangeSerializable(attributeChange);
            }

            CSEntryChange csentry = obj as CSEntryChange;

            if (csentry != null)
            {
                return new CSEntryChangeSerializable(csentry);
            }

            CSEntryChangeResult csentryresult = obj as CSEntryChangeResult;

            if (csentryresult != null)
            {
                return new CSEntryChangeResultSerializable(csentryresult);
            }

            AnchorAttribute anchor = obj as AnchorAttribute;

            if (anchor != null)
            {
                return new AnchorAttributeSerializable(anchor);
            }

            SchemaAttribute schemaAttribute = obj as SchemaAttribute;

            if (schemaAttribute != null)
            {
                return new SchemaAttributeSerializable(schemaAttribute);
            }

            SchemaType schemaType = obj as SchemaType;

            if (schemaType != null)
            {
                return new SchemaTypeSerializable(schemaType);
            }

            Schema schema = obj as Schema;

            if (schema != null)
            {
                return new SchemaSerializable(schema);
            }

            object substitute = GetObjectToSerializeExtended(obj);

            if (substitute != null)
            {
                return substitute;
            }

            return obj;
        }

        public object GetDeserializedObject(object obj, Type targetType)
        {
            ValueChangeSerializable valueChange = obj as ValueChangeSerializable;

            if (valueChange != null)
            {
                return valueChange.GetObject();
            }

            AttributeChangeSerializable attributeChange = obj as AttributeChangeSerializable;

            if (attributeChange != null)
            {
                return attributeChange.GetObject();
            }

            CSEntryChangeSerializable csentry = obj as CSEntryChangeSerializable;

            if (csentry != null)
            {
                return csentry.GetObject();
            }

            CSEntryChangeResultSerializable csentryresult = obj as CSEntryChangeResultSerializable;

            if (csentryresult != null)
            {
                return csentryresult.GetObject();
            }

            AnchorAttributeSerializable anchor = obj as AnchorAttributeSerializable;

            if (anchor != null)
            {
                return anchor.GetObject();
            }

            SchemaAttributeSerializable schemaAttribute = obj as SchemaAttributeSerializable;

            if (schemaAttribute != null)
            {
                return schemaAttribute.GetObject();
            }

            SchemaTypeSerializable schemaType = obj as SchemaTypeSerializable;

            if (schemaType != null)
            {
                return schemaType.GetObject();
            }

            SchemaSerializable schema = obj as SchemaSerializable;

            if (schema != null)
            {
                return schema.GetObject();
            }

            object rematerialised = GetDeserializedExtended(obj);

            if (rematerialised != null)
            {
                return rematerialised;
            }

            return obj;
        }
    }
#endif

    // Substitution logic for the Phase-2 DTO set, shared by both framework branches. Kept outside the #if so a
    // single copy serves the net48 IDataContractSurrogate and the net8 ISerializationSurrogateProvider. Each
    // helper returns the substituted object, or null when obj is not one of these types (the caller then falls
    // through to its remaining cases / returns obj unchanged).
    internal static class MmsSerializationSurrogateExtensions
    {
        public static object GetObjectToSerializeExtended(object obj)
        {
            MACapabilities capabilities = obj as MACapabilities;

            if (capabilities != null)
            {
                return new MACapabilitiesSerializable(capabilities);
            }

            ConfigParameter configParameter = obj as ConfigParameter;

            if (configParameter != null)
            {
                return new ConfigParameterSerializable(configParameter);
            }

            ConfigParameterDefinition configParameterDefinition = obj as ConfigParameterDefinition;

            if (configParameterDefinition != null)
            {
                return new ConfigParameterDefinitionSerializable(configParameterDefinition);
            }

            ParameterValidationResult parameterValidationResult = obj as ParameterValidationResult;

            if (parameterValidationResult != null)
            {
                return new ParameterValidationResultSerializable(parameterValidationResult);
            }

            OpenImportConnectionRunStep openImport = obj as OpenImportConnectionRunStep;

            if (openImport != null)
            {
                return new OpenImportConnectionRunStepSerializable(openImport);
            }

            OpenExportConnectionRunStep openExport = obj as OpenExportConnectionRunStep;

            if (openExport != null)
            {
                return new OpenExportConnectionRunStepSerializable(openExport);
            }

            CloseImportConnectionRunStep closeImport = obj as CloseImportConnectionRunStep;

            if (closeImport != null)
            {
                return new CloseImportConnectionRunStepSerializable(closeImport);
            }

            CloseExportConnectionRunStep closeExport = obj as CloseExportConnectionRunStep;

            if (closeExport != null)
            {
                return new CloseExportConnectionRunStepSerializable(closeExport);
            }

            GetImportEntriesRunStep getImportEntries = obj as GetImportEntriesRunStep;

            if (getImportEntries != null)
            {
                return new GetImportEntriesRunStepSerializable(getImportEntries);
            }

            Partition partition = obj as Partition;

            if (partition != null)
            {
                return new PartitionSerializable(partition);
            }

            HierarchyNode hierarchyNode = obj as HierarchyNode;

            if (hierarchyNode != null)
            {
                return new HierarchyNodeSerializable(hierarchyNode);
            }

            ChangeTypeDescription changeTypeDescription = obj as ChangeTypeDescription;

            if (changeTypeDescription != null)
            {
                return new ChangeTypeDescriptionSerializable(changeTypeDescription);
            }

            // CSEntryIdentity / CSEntryIdentityAttribute are our own framework-owned password-path carriers
            // (not host MMS types), but they cross the same pipe, so they are substituted here too.
            CSEntryIdentity csentryIdentity = obj as CSEntryIdentity;

            if (csentryIdentity != null)
            {
                return new CSEntryIdentitySerializable(csentryIdentity);
            }

            CSEntryIdentityAttribute csentryIdentityAttribute = obj as CSEntryIdentityAttribute;

            if (csentryIdentityAttribute != null)
            {
                return new CSEntryIdentityAttributeSerializable(csentryIdentityAttribute);
            }

            return null;
        }

        public static object GetDeserializedExtended(object obj)
        {
            MACapabilitiesSerializable capabilities = obj as MACapabilitiesSerializable;

            if (capabilities != null)
            {
                return capabilities.GetObject();
            }

            ConfigParameterSerializable configParameter = obj as ConfigParameterSerializable;

            if (configParameter != null)
            {
                return configParameter.GetObject();
            }

            ConfigParameterDefinitionSerializable configParameterDefinition = obj as ConfigParameterDefinitionSerializable;

            if (configParameterDefinition != null)
            {
                return configParameterDefinition.GetObject();
            }

            ParameterValidationResultSerializable parameterValidationResult = obj as ParameterValidationResultSerializable;

            if (parameterValidationResult != null)
            {
                return parameterValidationResult.GetObject();
            }

            OpenImportConnectionRunStepSerializable openImport = obj as OpenImportConnectionRunStepSerializable;

            if (openImport != null)
            {
                return openImport.GetObject();
            }

            OpenExportConnectionRunStepSerializable openExport = obj as OpenExportConnectionRunStepSerializable;

            if (openExport != null)
            {
                return openExport.GetObject();
            }

            CloseImportConnectionRunStepSerializable closeImport = obj as CloseImportConnectionRunStepSerializable;

            if (closeImport != null)
            {
                return closeImport.GetObject();
            }

            CloseExportConnectionRunStepSerializable closeExport = obj as CloseExportConnectionRunStepSerializable;

            if (closeExport != null)
            {
                return closeExport.GetObject();
            }

            GetImportEntriesRunStepSerializable getImportEntries = obj as GetImportEntriesRunStepSerializable;

            if (getImportEntries != null)
            {
                return getImportEntries.GetObject();
            }

            PartitionSerializable partition = obj as PartitionSerializable;

            if (partition != null)
            {
                return partition.GetObject();
            }

            HierarchyNodeSerializable hierarchyNode = obj as HierarchyNodeSerializable;

            if (hierarchyNode != null)
            {
                return hierarchyNode.GetObject();
            }

            ChangeTypeDescriptionSerializable changeTypeDescription = obj as ChangeTypeDescriptionSerializable;

            if (changeTypeDescription != null)
            {
                return changeTypeDescription.GetObject();
            }

            CSEntryIdentitySerializable csentryIdentity = obj as CSEntryIdentitySerializable;

            if (csentryIdentity != null)
            {
                return csentryIdentity.GetObject();
            }

            CSEntryIdentityAttributeSerializable csentryIdentityAttribute = obj as CSEntryIdentityAttributeSerializable;

            if (csentryIdentityAttribute != null)
            {
                return csentryIdentityAttribute.GetObject();
            }

            return null;
        }
    }
}
