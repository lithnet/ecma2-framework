using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real GetImportEntriesRunStep (inbound, full-object import path). CustomData from the
    // ImportRunStep base; FullObjectEntries is the heavy payload, each element a full CSEntryChange
    // substituted by the surrogate.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class GetImportEntriesRunStepSerializable
#else
    public class GetImportEntriesRunStepSerializable
#endif
    {
        [DataMember]
        public string CustomData { get; set; }

        [DataMember]
        public IList<CSEntryChange> FullObjectEntries { get; set; }

        internal GetImportEntriesRunStepSerializable(GetImportEntriesRunStep step)
        {
            this.SetObject(step);
        }

        internal void SetObject(GetImportEntriesRunStep step)
        {
            this.CustomData = step.CustomData;
            this.FullObjectEntries = step.FullObjectEntries;
        }

        internal GetImportEntriesRunStep GetObject()
        {
            return new GetImportEntriesRunStep(this.FullObjectEntries, this.CustomData);
        }
    }
}
