using System.Collections.Generic;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Represents a single page of import results returned by the orchestrator.
    /// </summary>
    internal sealed class ImportPage
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ImportPage"/> class.
        /// </summary>
        /// <param name="entries">The entries contained in this page.</param>
        /// <param name="moreToImport">Whether more entries remain to be imported.</param>
        public ImportPage(IList<CSEntryChange> entries, bool moreToImport)
        {
            this.Entries = entries;
            this.MoreToImport = moreToImport;
        }

        /// <summary>
        /// Gets the list of CSEntryChange objects in this page.
        /// </summary>
        public IList<CSEntryChange> Entries { get; }

        /// <summary>
        /// Gets a value indicating whether there are more entries to import after this page.
        /// </summary>
        public bool MoreToImport { get; }
    }
}
