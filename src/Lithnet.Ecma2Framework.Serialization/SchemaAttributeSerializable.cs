using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel;
using Microsoft.MetadirectoryServices;
using Microsoft.MetadirectoryServices.DetachedObjectModel;

namespace Lithnet.Ecma2Framework.Serialization
{
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class SchemaAttributeSerializable
#else
    public class SchemaAttributeSerializable
#endif
    {
        [DataMember]
        public AttributeType DataType { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public AttributeOperation AllowedAttributeOperation { get; set; }

        [DataMember]
        public bool IsAnchor { get; set; }

        [DataMember]
        public bool IsMultiValued { get; set; }

        // HiddenByDefault is the only settable member on the host SchemaAttribute and is not set by any
        // factory; carry it and apply it after factory construction.
        [DataMember]
        public bool HiddenByDefault { get; set; }

        internal SchemaAttributeSerializable(SchemaAttribute attribute)
        {
            this.SetObject(attribute);
        }

        internal void SetObject(SchemaAttribute attribute)
        {
            this.DataType = attribute.DataType;
            this.Name = attribute.Name;
            this.AllowedAttributeOperation = attribute.AllowedAttributeOperation;
            this.IsAnchor = attribute.IsAnchor;
            this.IsMultiValued = attribute.IsMultiValued;
            this.HiddenByDefault = attribute.HiddenByDefault;
        }

        internal SchemaAttribute GetObject()
        {
            SchemaAttribute attribute;

            if (this.IsAnchor)
            {
                attribute = SchemaAttribute.CreateAnchorAttribute(this.Name, this.DataType, this.AllowedAttributeOperation);
            }
            else if (this.IsMultiValued)
            {
                attribute = SchemaAttribute.CreateMultiValuedAttribute(this.Name, this.DataType, this.AllowedAttributeOperation);
            }
            else
            {
                attribute = SchemaAttribute.CreateSingleValuedAttribute(this.Name, this.DataType, this.AllowedAttributeOperation);
            }

            attribute.HiddenByDefault = this.HiddenByDefault;
            return attribute;
        }
    }
}
