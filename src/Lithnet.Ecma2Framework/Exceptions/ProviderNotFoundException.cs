using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework
{
    [Serializable]
    public class ProviderNotFoundException : Exception
    {
        public ProviderNotFoundException()
        {
        }

        public ProviderNotFoundException(string message)
            : base(message)
        {
        }

        public ProviderNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected ProviderNotFoundException(
            SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}