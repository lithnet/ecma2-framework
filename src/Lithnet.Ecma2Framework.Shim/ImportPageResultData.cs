using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The nested <c>result</c> payload of a <c>GetImportPage</c> response. The member names match the
    /// PascalCase JSON that StreamJsonRpc (2.x default Newtonsoft formatter) emits for the worker's
    /// <c>ImportPageResult</c> POCO property names — verified against the live serialiser, not assumed.
    /// </summary>
    [DataContract]
    internal sealed class ImportPageResultData
    {
        /// <summary>
        /// The page entries as a serialised <c>List&lt;CSEntryChange&gt;</c> (real MMS type via
        /// <c>MmsPipeSerializer</c>).
        /// </summary>
        [DataMember(Name = "EntriesXml")]
        public string EntriesXml { get; set; }

        /// <summary>
        /// True when the worker has more entries to return on a subsequent call.
        /// </summary>
        [DataMember(Name = "MoreToImport")]
        public bool MoreToImport { get; set; }

        /// <summary>
        /// Optional page-level custom-data watermark.
        /// </summary>
        [DataMember(Name = "CustomData")]
        public string CustomData { get; set; }
    }
}
