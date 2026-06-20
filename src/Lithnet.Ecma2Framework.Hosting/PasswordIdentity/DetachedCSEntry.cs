using System;
using System.Collections.Generic;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A read-only <see cref="CSEntry"/> reconstructed from the password-path identity carried across the
    /// pipe. On a password change the host <c>CSEntry</c> is an identity carrier only, so this exposes the
    /// identity surface (DN, RDN, object type/class, MA name, present attributes) backed by the carried
    /// data; the behavioural members (provisioning, deprovisioning, connection state, mutation) are
    /// rules-extension semantics with no meaning here and fail loud.
    /// </summary>
    internal sealed class DetachedCSEntry : CSEntry
    {
        private readonly CSEntryIdentity identity;

        public DetachedCSEntry(CSEntryIdentity identity)
        {
            this.identity = identity;
        }

        public override ReferenceValue DN
        {
            get { return this.identity.DN == null ? null : new DetachedReferenceValue(this.identity.DN); }
            set { throw PasswordIdentityNotSupported.Member("CSEntry.DN (set)"); }
        }

        public override string RDN
        {
            get { return this.identity.RDN; }
            set { throw PasswordIdentityNotSupported.Member("CSEntry.RDN (set)"); }
        }

        public override string ObjectType
        {
            get { return this.identity.ObjectType; }
        }

        public override ValueCollection ObjectClass
        {
            get
            {
                List<Value> values = new List<Value>();

                foreach (string objectClass in this.identity.ObjectClass)
                {
                    values.Add(new DetachedValue(objectClass, AttributeType.String));
                }

                return new DetachedValueCollection(values);
            }

            set { throw PasswordIdentityNotSupported.Member("CSEntry.ObjectClass (set)"); }
        }

        public override ManagementAgent MA
        {
            get { return this.identity.MAName == null ? null : new DetachedManagementAgent(this.identity.MAName); }
        }

        public override Attrib this[string attributeName]
        {
            get { return new DetachedAttrib(attributeName, this.identity[attributeName]); }
        }

        public override AttributeNameEnumerator GetEnumerator()
        {
            List<string> names = new List<string>();

            foreach (CSEntryIdentityAttribute attribute in this.identity.Attributes)
            {
                names.Add(attribute.Name);
            }

            return new DetachedAttributeNameEnumerator(names);
        }

        public override string ToString()
        {
            return this.identity.DN;
        }

        public override ConnectionState ConnectionState
        {
            get { throw PasswordIdentityNotSupported.Member("CSEntry.ConnectionState"); }
        }

        public override RuleType ConnectionRule
        {
            get { throw PasswordIdentityNotSupported.Member("CSEntry.ConnectionRule"); }
        }

        public override DateTime ConnectionChangeTime
        {
            get { throw PasswordIdentityNotSupported.Member("CSEntry.ConnectionChangeTime"); }
        }

        public override void CommitNewConnector()
        {
            throw PasswordIdentityNotSupported.Member("CSEntry.CommitNewConnector");
        }

        public override void Deprovision()
        {
            throw PasswordIdentityNotSupported.Member("CSEntry.Deprovision");
        }
    }
}
