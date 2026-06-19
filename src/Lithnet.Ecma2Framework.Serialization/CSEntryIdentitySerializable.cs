using System.Collections.Generic;
using System.Runtime.Serialization;
using Lithnet.Ecma2Framework;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries a CSEntryIdentity (our own framework-owned password-path identity object, NOT a host CSEntry).
    // It is a plain data object, so there is no Detached rebuild: GetObject news up the carrier and copies
    // every field, including the present attributes WITH their values (the GAP-7 fix). Crosses one direction
    // only (shim -> worker -> provider); the provider only reads it.
    //
    // Attributes is typed as the REAL CSEntryIdentityAttribute carrier (matching the established DTO pattern,
    // e.g. CSEntryChangeSerializable holding IList<AttributeChange>); the surrogate substitutes each element
    // for its CSEntryIdentityAttributeSerializable DTO during (de)serialization.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class CSEntryIdentitySerializable
#else
    public class CSEntryIdentitySerializable
#endif
    {
        [DataMember]
        public string DN { get; set; }

        [DataMember]
        public string RDN { get; set; }

        [DataMember]
        public string ObjectType { get; set; }

        [DataMember]
        public List<string> ObjectClass { get; set; }

        [DataMember]
        public string MAName { get; set; }

        [DataMember]
        public IList<CSEntryIdentityAttribute> Attributes { get; set; }

        public CSEntryIdentitySerializable()
        {
        }

        internal CSEntryIdentitySerializable(CSEntryIdentity identity)
        {
            this.SetObject(identity);
        }

        internal void SetObject(CSEntryIdentity identity)
        {
            this.DN = identity.DN;
            this.RDN = identity.RDN;
            this.ObjectType = identity.ObjectType;
            this.ObjectClass = new List<string>(identity.ObjectClass);
            this.MAName = identity.MAName;
            this.Attributes = new List<CSEntryIdentityAttribute>(identity.Attributes);
        }

        internal CSEntryIdentity GetObject()
        {
            CSEntryIdentity identity = new CSEntryIdentity
            {
                DN = this.DN,
                RDN = this.RDN,
                ObjectType = this.ObjectType,
                MAName = this.MAName,
            };

            if (this.ObjectClass != null)
            {
                foreach (string objectClass in this.ObjectClass)
                {
                    identity.ObjectClass.Add(objectClass);
                }
            }

            if (this.Attributes != null)
            {
                foreach (CSEntryIdentityAttribute attribute in this.Attributes)
                {
                    identity.AddAttribute(attribute);
                }
            }

            return identity;
        }
    }
}
