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
    internal class SchemaTypeSerializable
#else
    public class SchemaTypeSerializable
#endif
    {
        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public IList<SchemaAttribute> Attributes { get; set; }

        [DataMember]
        public bool Locked { get; set; }

        // A real mutable list backing field on the host (not a derived view); carry it so it round-trips.
        [DataMember]
        public IList<string> PossibleDNComponentsForProvisioning { get; set; }

        internal SchemaTypeSerializable(SchemaType type)
        {
            this.SetObject(type);
        }

        internal void SetObject(SchemaType type)
        {
            this.Attributes = type.Attributes;
            this.Locked = type.Locked;
            this.Name = type.Name;
            this.PossibleDNComponentsForProvisioning = type.PossibleDNComponentsForProvisioning;
        }

        internal SchemaType GetObject()
        {
            SchemaType t = SchemaType.Create(this.Name, this.Locked);

            if (this.Attributes != null)
            {
                foreach (SchemaAttribute a in this.Attributes)
                {
                    t.Attributes.Add(a);
                }
            }

            if (this.PossibleDNComponentsForProvisioning != null)
            {
                foreach (string component in this.PossibleDNComponentsForProvisioning)
                {
                    t.PossibleDNComponentsForProvisioning.Add(component);
                }
            }

            return t;
        }
    }
}
