using System.Collections.Concurrent;
using System.Threading;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// A class used to wrap a BlockingCollection of CSEntryChange objects, so that the user cannot access the collection directly
    /// </summary>
    internal class CSEntryChangeBlockingCollectionWrapper : ICSEntryChangeCollection
    {
        private readonly BlockingCollection<CSEntryChange> collection;
        private readonly CancellationToken cancellationToken;

        public CSEntryChangeBlockingCollectionWrapper(BlockingCollection<CSEntryChange> collection, CancellationToken cancellationToken)
        {
            this.collection = collection;
            this.cancellationToken = cancellationToken;
        }

        public void AddCSEntryChange(CSEntryChange csentry)
        {
            this.collection.Add(csentry, this.cancellationToken);
        }
    }
}
