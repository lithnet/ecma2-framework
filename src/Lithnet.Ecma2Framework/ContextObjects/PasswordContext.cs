using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public class PasswordContext
    {
        public Partition Partition { get; internal set; }

        public object CustomData { get; set; }
    }
}
