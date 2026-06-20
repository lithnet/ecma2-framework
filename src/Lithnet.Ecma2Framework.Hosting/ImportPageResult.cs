namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// The string-only transport carrier for a single <c>GetImportPage</c> result. The page's real
    /// <c>CSEntryChange</c> entries cross as a <see cref="MmsPipeSerializer"/> XML string in
    /// <see cref="EntriesXml"/>; <see cref="MoreToImport"/> and <see cref="CustomData"/> are plain
    /// scalars. StreamJsonRpc serialises this POCO on the worker side; the shim client parses the
    /// matching members from the JSON-RPC <c>result</c> object.
    /// </summary>
    /// <remarks>
    /// This is a thin transport envelope, not a mirror DTO: it carries no structured object-model
    /// data, only the serialised real-type payload plus the two paging scalars.
    /// </remarks>
    internal sealed class ImportPageResult
    {
        /// <summary>
        /// The page entries as a serialised <c>List&lt;CSEntryChange&gt;</c> (real MMS type via
        /// <see cref="Lithnet.Ecma2Framework.Serialization.MmsPipeSerializer"/>).
        /// </summary>
        public string EntriesXml { get; set; }

        /// <summary>
        /// True when the worker has more entries to return on a subsequent <c>GetImportPage</c> call.
        /// </summary>
        public bool MoreToImport { get; set; }

        /// <summary>
        /// Optional page-level custom-data watermark. Null when none is produced.
        /// </summary>
        public string CustomData { get; set; }
    }
}
