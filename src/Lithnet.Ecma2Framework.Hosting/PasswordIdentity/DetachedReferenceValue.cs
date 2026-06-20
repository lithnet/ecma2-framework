using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A read-only <see cref="ReferenceValue"/> backing a distinguished-name string carried on the
    /// password-path identity. Only the string form is available; the engine-bound DN-navigation members
    /// (component access, concat, parent, subcomponents) fail loud.
    /// </summary>
    internal sealed class DetachedReferenceValue : ReferenceValue
    {
        private readonly string dn;

        public DetachedReferenceValue(string dn)
        {
            this.dn = dn;
        }

        public override AttributeType DataType
        {
            get { return AttributeType.Reference; }
        }

        public override string ToString()
        {
            return this.dn;
        }

        public override bool Equals(object obj)
        {
            DetachedReferenceValue other = obj as DetachedReferenceValue;
            return other != null && string.Equals(this.dn, other.dn);
        }

        public override int GetHashCode()
        {
            return this.dn == null ? 0 : this.dn.GetHashCode();
        }

        public override long ToInteger()
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.ToInteger");
        }

        public override byte[] ToBinary()
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.ToBinary");
        }

        public override bool ToBoolean()
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.ToBoolean");
        }

        public override int Depth
        {
            get { throw PasswordIdentityNotSupported.Member("ReferenceValue.Depth"); }
        }

        public override string this[int componentIndex]
        {
            get { throw PasswordIdentityNotSupported.Member("ReferenceValue[componentIndex]"); }
        }

        public override ReferenceValue Concat(ReferenceValue dn)
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.Concat");
        }

        public override ReferenceValue Concat(string dn)
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.Concat");
        }

        public override ReferenceValue Parent()
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.Parent");
        }

        public override ReferenceValue Parent(int skipLevels)
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.Parent");
        }

        public override ReferenceValue Subcomponents(int startingComponent, int endingComponent)
        {
            throw PasswordIdentityNotSupported.Member("ReferenceValue.Subcomponents");
        }
    }
}
