using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // The host ChangeTypeDescription is abstract with only a protected ctor; its concrete impl is
    // engine-internal and not constructible by a consumer. This detached subclass reproduces the four
    // read-only strings so Schema.ChangeType can be carried and rebuilt host-side, matching the Detached*
    // pattern the host uses for its other abstract data types.
    internal sealed class ChangeTypeDescriptionDetached : ChangeTypeDescription
    {
        private readonly string attributeName;
        private readonly string add;
        private readonly string modify;
        private readonly string delete;

        public ChangeTypeDescriptionDetached(string attributeName, string add, string modify, string delete)
        {
            this.attributeName = attributeName;
            this.add = add;
            this.modify = modify;
            this.delete = delete;
        }

        public override string AttributeName => this.attributeName;

        public override string Add => this.add;

        public override string Modify => this.modify;

        public override string Delete => this.delete;
    }
}
