using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real CloseImportConnectionRunStep (inbound). Reason carried explicitly so the host default
    // ctor's Reason=Normal is never re-applied over a carried value. CustomData from the ImportRunStep base.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class CloseImportConnectionRunStepSerializable
#else
    public class CloseImportConnectionRunStepSerializable
#endif
    {
        [DataMember]
        public string CustomData { get; set; }

        [DataMember]
        public CloseReason Reason { get; set; }

        internal CloseImportConnectionRunStepSerializable(CloseImportConnectionRunStep step)
        {
            this.SetObject(step);
        }

        internal void SetObject(CloseImportConnectionRunStep step)
        {
            this.CustomData = step.CustomData;
            this.Reason = step.Reason;
        }

        internal CloseImportConnectionRunStep GetObject()
        {
            return new CloseImportConnectionRunStep(this.Reason, this.CustomData);
        }
    }
}
