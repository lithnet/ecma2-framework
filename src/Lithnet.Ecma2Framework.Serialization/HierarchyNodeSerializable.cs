using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real HierarchyNode. Two read-only strings; an element of the inclusion/exclusion lists on
    // the open run-steps (inbound) and returned from GetHierarchy (outbound), so GetObject rebuilds it.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class HierarchyNodeSerializable
#else
    public class HierarchyNodeSerializable
#endif
    {
        [DataMember]
        public string DN { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        internal HierarchyNodeSerializable(HierarchyNode node)
        {
            this.SetObject(node);
        }

        internal void SetObject(HierarchyNode node)
        {
            this.DN = node.DN;
            this.DisplayName = node.DisplayName;
        }

        internal HierarchyNode GetObject()
        {
            return HierarchyNode.Create(this.DN, this.DisplayName);
        }
    }
}
