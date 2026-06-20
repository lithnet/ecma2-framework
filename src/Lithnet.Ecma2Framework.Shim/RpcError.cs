using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The JSON-RPC 2.0 <c>error</c> object. See: https://www.jsonrpc.org/specification#error_object
    /// </summary>
    [DataContract]
    internal sealed class RpcError
    {
        [DataMember(Name = "code")]
        public int Code { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        /// <summary>
        /// The structured error data the worker attached (Path C exception marshalling). When the worker's
        /// handler threw, this carries the serialised <c>MmsExceptionEnvelope</c> JSON string (set on the
        /// StreamJsonRpc <c>LocalRpcException.ErrorData</c>), which the shim deserialises and reconstructs into
        /// the EXACT real host exception. Null for transport/framing errors that carry no envelope.
        /// </summary>
        [DataMember(Name = "data", IsRequired = false, EmitDefaultValue = false)]
        public string Data { get; set; }
    }
}
