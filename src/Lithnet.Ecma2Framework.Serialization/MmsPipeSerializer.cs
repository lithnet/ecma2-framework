using System;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace Lithnet.Ecma2Framework.Serialization
{
    // The single serialization entry point for carrying REAL Microsoft.MetadirectoryServices
    // object graphs across the worker/shim pipe. A DataContractSerializer is built rooted at the
    // requested real abstract type (e.g. typeof(CSEntryChange)) with the eight surrogate DTOs as
    // known types and MmsSerializationSurrogate attached. The surrogate is wired per runtime:
    // net8 via SetSerializationSurrogateProvider, net4x via DataContractSurrogate.
#if ECMA2_SHIM_INTERNAL
    internal static class MmsPipeSerializer
#else
    public static class MmsPipeSerializer
#endif
    {
        private static readonly Type[] KnownTypes = new Type[]
        {
            typeof(CSEntryChangeSerializable),
            typeof(AttributeChangeSerializable),
            typeof(ValueChangeSerializable),
            typeof(AnchorAttributeSerializable),
            typeof(SchemaSerializable),
            typeof(SchemaTypeSerializable),
            typeof(SchemaAttributeSerializable),
            typeof(CSEntryChangeResultSerializable),
            typeof(MACapabilitiesSerializable),
            typeof(ConfigParameterSerializable),
            typeof(ConfigParameterDefinitionSerializable),
            typeof(ParameterValidationResultSerializable),
            typeof(OpenImportConnectionRunStepSerializable),
            typeof(OpenExportConnectionRunStepSerializable),
            typeof(CloseImportConnectionRunStepSerializable),
            typeof(CloseExportConnectionRunStepSerializable),
            typeof(GetImportEntriesRunStepSerializable),
            typeof(PartitionSerializable),
            typeof(HierarchyNodeSerializable),
            typeof(ChangeTypeDescriptionSerializable),
            typeof(CSEntryIdentitySerializable),
            typeof(CSEntryIdentityAttributeSerializable),
        };

        private static DataContractSerializer CreateSerializer(Type rootType)
        {
            if (rootType == null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

#if NETFRAMEWORK
            DataContractSerializerSettings settings = new DataContractSerializerSettings();
            settings.KnownTypes = KnownTypes;
            settings.DataContractSurrogate = new MmsSerializationSurrogate();
            return new DataContractSerializer(rootType, settings);
#else
            DataContractSerializer serializer = new DataContractSerializer(rootType, KnownTypes);
            serializer.SetSerializationSurrogateProvider(new MmsSerializationSurrogate());
            return serializer;
#endif
        }

        public static string SerializeXml<T>(T graph)
        {
            return SerializeXml(graph, typeof(T));
        }

        public static string SerializeXml(object graph, Type rootType)
        {
            DataContractSerializer serializer = CreateSerializer(rootType);

            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, graph);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static T DeserializeXml<T>(string xml)
        {
            return (T)DeserializeXml(xml, typeof(T));
        }

        public static object DeserializeXml(string xml, Type rootType)
        {
            if (xml == null)
            {
                throw new ArgumentNullException(nameof(xml));
            }

            DataContractSerializer serializer = CreateSerializer(rootType);

            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return serializer.ReadObject(ms);
            }
        }
    }
}
