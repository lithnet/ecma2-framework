using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.ComponentModel;
using Microsoft.MetadirectoryServices;
using Microsoft.MetadirectoryServices.DetachedObjectModel;

namespace Lithnet.Ecma2Framework.Serialization
{
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class ValueChangeSerializable
#else
    public class ValueChangeSerializable
#endif
    {
        [DataMember]
        public object Value { get; set; }

        [DataMember]
        public ValueModificationType ModificationType { get; set; }

        internal ValueChangeSerializable(ValueChange change)
        {
            this.SetObject(change);
        }

        internal void SetObject(ValueChange change)
        {
            this.Value = change.Value;
            this.ModificationType = change.ModificationType;
        }

        internal ValueChange GetObject()
        {
            return new ValueChange(this.Value, this.ModificationType);
        }
    }
}
