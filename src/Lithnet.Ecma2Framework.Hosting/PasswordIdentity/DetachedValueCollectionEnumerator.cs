using System.Collections.Generic;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A forward-only enumerator over a fixed list of <see cref="Value"/> carried on the password-path identity.
    /// </summary>
    internal sealed class DetachedValueCollectionEnumerator : ValueCollectionEnumerator
    {
        private readonly IList<Value> values;
        private int index = -1;

        public DetachedValueCollectionEnumerator(IList<Value> values)
        {
            this.values = values;
        }

        public override bool MoveNext()
        {
            this.index++;
            return this.index < this.values.Count;
        }

        public override Value Current
        {
            get { return this.values[this.index]; }
        }

        public override void Reset()
        {
            this.index = -1;
        }
    }
}
