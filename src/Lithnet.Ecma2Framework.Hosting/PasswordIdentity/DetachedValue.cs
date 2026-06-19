using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A read-only <see cref="Value"/> backing a single boxed CLR value carried on the password-path
    /// identity. The typed conversion accessors return the carried value when its CLR type matches and
    /// fail loud otherwise.
    /// </summary>
    internal class DetachedValue : Value
    {
        private readonly object value;
        private readonly AttributeType dataType;

        public DetachedValue(object value, AttributeType dataType)
        {
            this.value = value;
            this.dataType = dataType;
        }

        protected object BackingValue
        {
            get { return this.value; }
        }

        public override AttributeType DataType
        {
            get { return this.dataType; }
        }

        public override string ToString()
        {
            return this.value == null ? null : this.value.ToString();
        }

        public override long ToInteger()
        {
            if (this.value is long)
            {
                return (long)this.value;
            }

            throw PasswordIdentityNotSupported.Member("Value.ToInteger");
        }

        public override byte[] ToBinary()
        {
            if (this.value is byte[])
            {
                return (byte[])this.value;
            }

            throw PasswordIdentityNotSupported.Member("Value.ToBinary");
        }

        public override bool ToBoolean()
        {
            if (this.value is bool)
            {
                return (bool)this.value;
            }

            throw PasswordIdentityNotSupported.Member("Value.ToBoolean");
        }

        public override bool Equals(object obj)
        {
            DetachedValue other = obj as DetachedValue;
            return other != null && object.Equals(this.value, other.value);
        }

        public override int GetHashCode()
        {
            return this.value == null ? 0 : this.value.GetHashCode();
        }
    }
}
