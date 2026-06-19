using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Serializes an MmsExceptionEnvelope to/from a self-contained JSON string using the BCL
    // DataContractJsonSerializer (no third-party dependency, so it compiles into the dependency-free shim).
    // The string form crosses the pipe inside the JSON-RPC error 'data' member: the worker sets it on the
    // StreamJsonRpc LocalRpcException.ErrorData; the shim reads error.data as a string and rehydrates it here.
    // A string is encoded identically by every JSON-RPC formatter, so the worker formatter choice does not
    // affect the contract.
#if ECMA2_SHIM_INTERNAL
    internal static class MmsExceptionEnvelopeSerializer
#else
    public static class MmsExceptionEnvelopeSerializer
#endif
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string Serialize(MmsExceptionEnvelope envelope)
        {
            if (envelope == null)
            {
                return null;
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(MmsExceptionEnvelope));

            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, envelope);
                return Utf8NoBom.GetString(ms.ToArray());
            }
        }

        public static MmsExceptionEnvelope Deserialize(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(MmsExceptionEnvelope));

            using (MemoryStream ms = new MemoryStream(Utf8NoBom.GetBytes(json)))
            {
                return (MmsExceptionEnvelope)serializer.ReadObject(ms);
            }
        }
    }
}
