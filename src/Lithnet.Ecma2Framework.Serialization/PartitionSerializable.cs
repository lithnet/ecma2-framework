using System;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real Partition. Crosses both directions (argument into open run-steps; returned from
    // GetPartitions), so GetObject must rebuild faithfully. HiddenByDefault is a documented prior wire-drop
    // hazard and is carried explicitly. Partition.Create(Identifier, DN, Name) defaults an empty DN to the
    // identifier string; the DN is carried verbatim so a real DN is never replaced by that authoring default.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class PartitionSerializable
#else
    public class PartitionSerializable
#endif
    {
        [DataMember]
        public Guid Identifier { get; set; }

        [DataMember]
        public string DN { get; set; }

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public bool HiddenByDefault { get; set; }

        internal PartitionSerializable(Partition partition)
        {
            this.SetObject(partition);
        }

        internal void SetObject(Partition partition)
        {
            this.Identifier = partition.Identifier;
            this.DN = partition.DN;
            this.Name = partition.Name;
            this.HiddenByDefault = partition.HiddenByDefault;
        }

        internal Partition GetObject()
        {
            Partition partition = Partition.Create(this.Identifier, this.DN, this.Name);
            partition.HiddenByDefault = this.HiddenByDefault;
            return partition;
        }
    }
}
