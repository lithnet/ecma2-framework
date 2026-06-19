using System.Collections.Generic;
using System.Runtime.Serialization;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries a CSEntryIdentityAttribute (our own carrier, not a host Attrib). Plain data: the value list is
    // a List<object> of boxed CLR primitives (string/long/bool/byte[]/reference-as-string), which the
    // DataContract serializer preserves with their boxed type, matching the ValueChangeSerializable.Value
    // pattern. GetObject news up the carrier directly.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class CSEntryIdentityAttributeSerializable
#else
    public class CSEntryIdentityAttributeSerializable
#endif
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public AttributeType DataType { get; set; }

        [DataMember]
        public bool IsMultivalued { get; set; }

        [DataMember]
        public List<object> Values { get; set; }

        public CSEntryIdentityAttributeSerializable()
        {
        }

        internal CSEntryIdentityAttributeSerializable(CSEntryIdentityAttribute attribute)
        {
            this.SetObject(attribute);
        }

        internal void SetObject(CSEntryIdentityAttribute attribute)
        {
            this.Name = attribute.Name;
            this.DataType = attribute.DataType;
            this.IsMultivalued = attribute.IsMultivalued;
            this.Values = new List<object>(attribute.Values);
        }

        internal CSEntryIdentityAttribute GetObject()
        {
            return new CSEntryIdentityAttribute(this.Name, this.DataType, this.IsMultivalued, this.Values);
        }
    }
}
