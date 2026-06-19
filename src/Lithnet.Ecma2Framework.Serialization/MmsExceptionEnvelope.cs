using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Serialization
{
    // A faithful, uniform carrier for an exception thrown inside the worker, sent across the JSON-RPC error
    // path so the shim can re-throw the EXACT real host exception (Path C / mapping Section D).
    //
    // The envelope records a TypeName discriminator, the base Message, the declared Tree-A field values
    // (whichever the concrete host type declares), and a recursive InnerException envelope (the chain; an
    // inner may itself be a host exception or a non-host worker exception). IsHostException distinguishes a
    // host MetadirectoryServicesException/ExtensionException subclass (reconstructed as the exact host type)
    // from a non-host worker exception (surfaced as a host ExtensibleExtensionException carrier preserving
    // the concrete type name + message + inner chain).
    //
    // SECURITY: this envelope is built from an already-thrown exception. The password path constructs its
    // provider exceptions secret-free by design; the envelope copies only the type name, the host-declared
    // diagnostic fields, the (secret-free) Message, and the inner chain. No password value is ever read into
    // it.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class MmsExceptionEnvelope
#else
    public class MmsExceptionEnvelope
#endif
    {
        // The exact concrete type name. For a host exception this is the simple name of one of the 42 host
        // exception types (e.g. "WarningNoWatermarkException"); for a non-host carrier this is the concrete
        // CLR type's full name (e.g. "System.Net.Http.HttpRequestException").
        [DataMember]
        public string TypeName { get; set; }

        // True when TypeName names one of the 42 host exception types and the shim should reconstruct the
        // exact host type. False for a non-host worker exception (surfaced via the carrier).
        [DataMember]
        public bool IsHostException { get; set; }

        // The base Exception.Message, carried verbatim (never re-derived from a ctor format string).
        [DataMember]
        public string Message { get; set; }

        // Declared Tree-A backing-field values. Only the field(s) the concrete host type declares are set;
        // the rest stay null. Mapping (mapping Section D):
        //   m_attributeName  -> AttributeName
        //   m_MAName         -> MAName
        //   m_DN             -> Dn
        //   m_ObjectClasses  -> ObjectClasses
        //   m_PrimaryObjectClass (getter ObjectType) -> ObjectType
        //   m_className      -> ClassName (NoSuchObjectTypeException getter is ObjectType, field is m_className)
        //   m_parameterName  -> ParameterName
        [DataMember]
        public string AttributeName { get; set; }

        [DataMember]
        public string MAName { get; set; }

        [DataMember]
        public string Dn { get; set; }

        [DataMember]
        public string[] ObjectClasses { get; set; }

        [DataMember]
        public string ObjectType { get; set; }

        [DataMember]
        public string ClassName { get; set; }

        [DataMember]
        public string ParameterName { get; set; }

        // The recursive inner-exception chain, or null at the end of the chain.
        [DataMember]
        public MmsExceptionEnvelope InnerException { get; set; }
    }
}
