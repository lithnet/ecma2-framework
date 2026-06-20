using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real ParameterValidationResult. Three plain auto-properties, no guards or side-effects.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class ParameterValidationResultSerializable
#else
    public class ParameterValidationResultSerializable
#endif
    {
        [DataMember]
        public ParameterValidationResultCode Code { get; set; }

        [DataMember]
        public string ErrorMessage { get; set; }

        [DataMember]
        public string ErrorParameter { get; set; }

        internal ParameterValidationResultSerializable(ParameterValidationResult result)
        {
            this.SetObject(result);
        }

        internal void SetObject(ParameterValidationResult result)
        {
            this.Code = result.Code;
            this.ErrorMessage = result.ErrorMessage;
            this.ErrorParameter = result.ErrorParameter;
        }

        internal ParameterValidationResult GetObject()
        {
            return new ParameterValidationResult(this.Code, this.ErrorMessage, this.ErrorParameter);
        }
    }
}
