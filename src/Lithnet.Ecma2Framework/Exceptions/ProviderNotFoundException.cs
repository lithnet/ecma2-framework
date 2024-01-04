using System;
using System.Runtime.Serialization;

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