using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The base JSON-RPC 2.0 response envelope. Carries the <c>error</c> object common to every
    /// response; concrete subclasses add the typed <c>result</c> member. Only the members this shim
    /// reads are modelled; <see cref="System.Runtime.Serialization.Json.DataContractJsonSerializer"/>
    /// silently ignores unknown members (e.g. <c>jsonrpc</c>, <c>id</c>).
    /// </summary>
    [DataContract]
    [KnownType(typeof(StringResultEnvelope))]
    [KnownType(typeof(BoolResultEnvelope))]
    [KnownType(typeof(ImportPageResultEnvelope))]
    internal abstract class RpcResponseEnvelope
    {
        /// <summary>
        /// The error object, present when the worker returned a JSON-RPC error response; null on success.
        /// </summary>
        [DataMember(Name = "error")]
        public RpcError Error { get; set; }
    }
}
