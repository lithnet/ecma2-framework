using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real OpenImportConnectionRunStep (inbound, FIM -> worker). All members carried explicitly so
    // the host default ctor's ImportType=Full / PageSize=1 is never re-applied over a faithfully-carried
    // value. Nested Partition and HierarchyNode lists are substituted by the surrogate. CustomData is the one
    // field contributed by the ImportRunStep base.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class OpenImportConnectionRunStepSerializable
#else
    public class OpenImportConnectionRunStepSerializable
#endif
    {
        [DataMember]
        public string CustomData { get; set; }

        [DataMember]
        public int PageSize { get; set; }

        [DataMember]
        public OperationType ImportType { get; set; }

        [DataMember]
        public Partition StepPartition { get; set; }

        [DataMember]
        public IList<HierarchyNode> InclusionHierarchyNodes { get; set; }

        [DataMember]
        public IList<HierarchyNode> ExclusionHierarchyNodes { get; set; }

        internal OpenImportConnectionRunStepSerializable(OpenImportConnectionRunStep step)
        {
            this.SetObject(step);
        }

        internal void SetObject(OpenImportConnectionRunStep step)
        {
            this.CustomData = step.CustomData;
            this.PageSize = step.PageSize;
            this.ImportType = step.ImportType;
            this.StepPartition = step.StepPartition;
            this.InclusionHierarchyNodes = step.InclusionHierarchyNodes;
            this.ExclusionHierarchyNodes = step.ExclusionHierarchyNodes;
        }

        internal OpenImportConnectionRunStep GetObject()
        {
            return new OpenImportConnectionRunStep(
                this.StepPartition,
                this.ImportType,
                this.PageSize,
                this.CustomData,
                this.InclusionHierarchyNodes,
                this.ExclusionHierarchyNodes);
        }
    }
}
