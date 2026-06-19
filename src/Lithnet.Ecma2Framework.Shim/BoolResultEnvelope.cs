using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// A JSON-RPC response envelope whose <c>result</c> is a boolean acknowledgement.
    /// </summary>
    [DataContract]
    internal sealed class BoolResultEnvelope : RpcResponseEnvelope
    {
        [DataMember(Name = "result")]
        public bool Result { get; set; }
    }
}
