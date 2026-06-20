using System;
using System.Collections.Generic;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A read-only <see cref="Attrib"/> backed by a single present attribute carried on the password-path
    /// identity (or a not-present placeholder for an attribute the entry does not carry). The typed value
    /// accessors return the carried value(s); the setters, <c>Delete</c>, and engine-only metadata fail loud.
    /// </summary>
    internal sealed class DetachedAttrib : Attrib
    {
        private readonly string name;
        private readonly CSEntryIdentityAttribute attribute;

        /// <summary>
        /// Creates an attribute accessor. A null <paramref name="attribute"/> represents an attribute that
        /// is not present on the entry (the host returns a non-present <see cref="Attrib"/> for an unknown name).
        /// </summary>
        public DetachedAttrib(string name, CSEntryIdentityAttribute attribute)
        {
            this.name = name;
            this.attribute = attribute;
        }

        public override string Name
        {
            get { return this.name; }
        }

        public override bool IsPresent
        {
            get { return this.attribute != null; }
        }

        public override AttributeType DataType
        {
            get { return this.attribute == null ? AttributeType.Undefined : this.attribute.DataType; }
        }

        public override bool IsMultivalued
        {
            get { return this.attribute != null && this.attribute.IsMultivalued; }
        }

        public override string Value
        {
            get
            {
                object v = this.FirstValue();
                return v == null ? null : v.ToString();
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.Value (set)"); }
        }

        public override string StringValue
        {
            get
            {
                object v = this.FirstValue();
                return v == null ? null : v.ToString();
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.StringValue (set)"); }
        }

        public override long IntegerValue
        {
            get
            {
                object v = this.FirstValue();

                if (v == null)
                {
                    return 0;
                }

                if (v is long)
                {
                    return (long)v;
                }

                throw PasswordIdentityNotSupported.Member("Attrib.IntegerValue (attribute is not an integer)");
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.IntegerValue (set)"); }
        }

        public override byte[] BinaryValue
        {
            get
            {
                object v = this.FirstValue();

                if (v == null)
                {
                    return null;
                }

                if (v is byte[])
                {
                    return (byte[])v;
                }

                throw PasswordIdentityNotSupported.Member("Attrib.BinaryValue (attribute is not binary)");
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.BinaryValue (set)"); }
        }

        public override bool BooleanValue
        {
            get
            {
                object v = this.FirstValue();

                if (v == null)
                {
                    return false;
                }

                if (v is bool)
                {
                    return (bool)v;
                }

                throw PasswordIdentityNotSupported.Member("Attrib.BooleanValue (attribute is not boolean)");
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.BooleanValue (set)"); }
        }

        public override ReferenceValue ReferenceValue
        {
            get
            {
                object v = this.FirstValue();
                return v == null ? null : new DetachedReferenceValue(v.ToString());
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.ReferenceValue (set)"); }
        }

        public override ValueCollection Values
        {
            get
            {
                List<Value> values = new List<Value>();

                if (this.attribute != null)
                {
                    foreach (object v in this.attribute.Values)
                    {
                        values.Add(new DetachedValue(v, this.attribute.DataType));
                    }
                }

                return new DetachedValueCollection(values);
            }

            set { throw PasswordIdentityNotSupported.Member("Attrib.Values (set)"); }
        }

        public override ManagementAgent LastContributingMA
        {
            get { throw PasswordIdentityNotSupported.Member("Attrib.LastContributingMA"); }
        }

        public override DateTime LastContributionTime
        {
            get { throw PasswordIdentityNotSupported.Member("Attrib.LastContributionTime"); }
        }

        public override void Delete()
        {
            throw PasswordIdentityNotSupported.Member("Attrib.Delete");
        }

        private object FirstValue()
        {
            if (this.attribute == null || this.attribute.Values.Count == 0)
            {
                return null;
            }

            return this.attribute.Values[0];
        }
    }
}
