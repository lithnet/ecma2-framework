using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real CloseExportConnectionRunStep (inbound). Structurally distinct from the import close
    // step: it does NOT derive from a run-step base and has NO CustomData. Reason carried explicitly so the
    // host default ctor's Reason=Normal is never re-applied.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class CloseExportConnectionRunStepSerializable
#else
    public class CloseExportConnectionRunStepSerializable
#endif
    {
        [DataMember]
        public CloseReason Reason { get; set; }

        internal CloseExportConnectionRunStepSerializable(CloseExportConnectionRunStep step)
        {
            this.SetObject(step);
        }

        internal void SetObject(CloseExportConnectionRunStep step)
        {
            this.Reason = step.Reason;
        }

        internal CloseExportConnectionRunStep GetObject()
        {
            return new CloseExportConnectionRunStep(this.Reason);
        }
    }
}
