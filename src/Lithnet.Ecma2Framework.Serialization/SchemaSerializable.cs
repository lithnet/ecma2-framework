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
    internal class SchemaSerializable
#else
    public class SchemaSerializable
#endif
    {
        [DataMember]
        public IList<SchemaType> Types{ get; set; }

        // The host change-type description (nullable) and the object-type attribute name. Carried so a Schema
        // that declares them round-trips faithfully; the host Schema.Create() leaves both null.
        [DataMember]
        public ChangeTypeDescription ChangeType { get; set; }

        [DataMember]
        public string ObjectTypeAttributeName { get; set; }

        internal SchemaSerializable(Schema s)
        {
            this.SetObject(s);
        }

        internal void SetObject(Schema s)
        {
            this.Types = s.Types;
            this.ChangeType = s.ChangeType;
            this.ObjectTypeAttributeName = s.ObjectTypeAttributeName;
        }

        internal Schema GetObject()
        {
            Schema s = new Schema(this.ChangeType, this.ObjectTypeAttributeName);

            if (this.Types != null)
            {
                foreach(SchemaType t in this.Types)
                {
                    s.Types.Add(t);
                }
            }

            return s;
        }
    }
}
