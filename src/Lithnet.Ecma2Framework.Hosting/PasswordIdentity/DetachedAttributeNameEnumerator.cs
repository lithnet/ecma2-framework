using System.Collections.Generic;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A forward-only enumerator over the present attribute names of a password-path identity entry.
    /// </summary>
    internal sealed class DetachedAttributeNameEnumerator : AttributeNameEnumerator
    {
        private readonly IList<string> names;
        private int index = -1;

        public DetachedAttributeNameEnumerator(IList<string> names)
        {
            this.names = names;
        }

        public override bool MoveNext()
        {
            this.index++;
            return this.index < this.names.Count;
        }

        public override string Current
        {
            get { return this.names[this.index]; }
        }

        public override void Reset()
        {
            this.index = -1;
        }
    }
}
