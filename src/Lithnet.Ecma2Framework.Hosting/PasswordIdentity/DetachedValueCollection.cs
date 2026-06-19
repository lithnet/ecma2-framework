using System.Collections.Generic;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A read-only <see cref="ValueCollection"/> over a fixed list of <see cref="Value"/> carried on the
    /// password-path identity (used for the object-class list and multi-valued attribute values). The
    /// mutating members fail loud — the identity entry is never written back to the engine.
    /// </summary>
    internal sealed class DetachedValueCollection : ValueCollection
    {
        private readonly IList<Value> values;

        public DetachedValueCollection(IList<Value> values)
        {
            this.values = values;
        }

        public override int Count
        {
            get { return this.values.Count; }
        }

        public override Value this[int index]
        {
            get { return this.values[index]; }
        }

        public override ValueCollectionEnumerator GetEnumerator()
        {
            return new DetachedValueCollectionEnumerator(this.values);
        }

        public override string[] ToStringArray()
        {
            string[] result = new string[this.values.Count];

            for (int i = 0; i < this.values.Count; i++)
            {
                result[i] = this.values[i] == null ? null : this.values[i].ToString();
            }

            return result;
        }

        public override long[] ToIntegerArray()
        {
            long[] result = new long[this.values.Count];

            for (int i = 0; i < this.values.Count; i++)
            {
                result[i] = this.values[i].ToInteger();
            }

            return result;
        }

        public override bool Contains(string val)
        {
            foreach (Value value in this.values)
            {
                if (value != null && string.Equals(value.ToString(), val))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool Contains(long val)
        {
            foreach (Value value in this.values)
            {
                if (value != null && value.DataType == AttributeType.Integer && value.ToInteger() == val)
                {
                    return true;
                }
            }

            return false;
        }

        public override bool Contains(byte[] val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Contains(byte[])");
        }

        public override bool Contains(Value val)
        {
            return val != null && this.values.Contains(val);
        }

        public override void Add(string val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Add");
        }

        public override void Add(long val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Add");
        }

        public override void Add(byte[] val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Add");
        }

        public override void Add(Value val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Add");
        }

        public override void Add(ValueCollection val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Add");
        }

        public override void Clear()
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Clear");
        }

        public override void Remove(string val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Remove");
        }

        public override void Remove(long val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Remove");
        }

        public override void Remove(byte[] val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Remove");
        }

        public override void Remove(Value val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Remove");
        }

        public override void Set(bool val)
        {
            throw PasswordIdentityNotSupported.Member("ValueCollection.Set");
        }
    }
}
