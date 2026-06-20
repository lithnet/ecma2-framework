using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real OpenExportConnectionRunStep (inbound, FIM -> worker). BatchSize and ExportType carried
    // explicitly so the host default ctor's BatchSize=1 is never re-applied. ExportType is contributed by the
    // ExportRunStep base. Nested Partition and HierarchyNode lists are substituted by the surrogate.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class OpenExportConnectionRunStepSerializable
#else
    public class OpenExportConnectionRunStepSerializable
#endif
    {
        [DataMember]
        public OperationType ExportType { get; set; }

        [DataMember]
        public int BatchSize { get; set; }

        [DataMember]
        public Partition StepPartition { get; set; }

        [DataMember]
        public IList<HierarchyNode> InclusionHierarchyNodes { get; set; }

        [DataMember]
        public IList<HierarchyNode> ExclusionHierarchyNodes { get; set; }

        internal OpenExportConnectionRunStepSerializable(OpenExportConnectionRunStep step)
        {
            this.SetObject(step);
        }

        internal void SetObject(OpenExportConnectionRunStep step)
        {
            this.ExportType = step.ExportType;
            this.BatchSize = step.BatchSize;
            this.StepPartition = step.StepPartition;
            this.InclusionHierarchyNodes = step.InclusionHierarchyNodes;
            this.ExclusionHierarchyNodes = step.ExclusionHierarchyNodes;
        }

        internal OpenExportConnectionRunStep GetObject()
        {
            return new OpenExportConnectionRunStep(
                this.StepPartition,
                this.BatchSize,
                this.ExportType,
                this.InclusionHierarchyNodes,
                this.ExclusionHierarchyNodes);
        }
    }
}
