using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// A JSON-RPC response envelope whose <c>result</c> is a single string (a <c>MmsPipeSerializer</c>
    /// XML payload, a watermark, or another scalar string value).
    /// </summary>
    [DataContract]
    internal sealed class StringResultEnvelope : RpcResponseEnvelope
    {
        [DataMember(Name = "result")]
        public string Result { get; set; }
    }
}
