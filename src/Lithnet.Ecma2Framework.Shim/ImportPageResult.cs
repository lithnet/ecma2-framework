namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The shim-side, deserialised result of a single <c>GetImportPage</c> call: the page's real
    /// <c>CSEntryChange</c> entries (still as the <c>MmsPipeSerializer</c> XML string in
    /// <see cref="EntriesXml"/>) plus the two paging scalars. The connection class deserialises
    /// <see cref="EntriesXml"/> into real host entries via <c>MmsPipeSerializer</c>.
    /// </summary>
    internal sealed class ImportPageResult
    {
        /// <summary>
        /// The page entries as a serialised <c>List&lt;CSEntryChange&gt;</c> (real MMS type).
        /// </summary>
        public string EntriesXml { get; set; }

        /// <summary>
        /// True when the worker has more entries to return on a subsequent call.
        /// </summary>
        public bool MoreToImport { get; set; }

        /// <summary>
        /// Optional page-level custom-data watermark. Null when none is produced.
        /// </summary>
        public string CustomData { get; set; }
    }
}
