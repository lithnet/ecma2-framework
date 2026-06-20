using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// A JSON-RPC response envelope for <c>GetImportPage</c>. The worker returns an
    /// <c>ImportPageResult</c> POCO; StreamJsonRpc serialises it (camelCase) as the nested JSON-RPC
    /// <c>result</c> object, so the result member is modelled here as <see cref="ImportPageResultData"/>.
    /// </summary>
    [DataContract]
    internal sealed class ImportPageResultEnvelope : RpcResponseEnvelope
    {
        [DataMember(Name = "result")]
        public ImportPageResultData Result { get; set; }
    }
}
