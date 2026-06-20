using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real ChangeTypeDescription (four read-only strings), a nested member of Schema. The host
    // type has no public ctor; GetObject rebuilds it via the detached subclass.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class ChangeTypeDescriptionSerializable
#else
    public class ChangeTypeDescriptionSerializable
#endif
    {
        [DataMember]
        public string AttributeName { get; set; }

        [DataMember]
        public string Add { get; set; }

        [DataMember]
        public string Modify { get; set; }

        [DataMember]
        public string Delete { get; set; }

        internal ChangeTypeDescriptionSerializable(ChangeTypeDescription description)
        {
            this.SetObject(description);
        }

        internal void SetObject(ChangeTypeDescription description)
        {
            this.AttributeName = description.AttributeName;
            this.Add = description.Add;
            this.Modify = description.Modify;
            this.Delete = description.Delete;
        }

        internal ChangeTypeDescription GetObject()
        {
            return new ChangeTypeDescriptionDetached(this.AttributeName, this.Add, this.Modify, this.Delete);
        }
    }
}
