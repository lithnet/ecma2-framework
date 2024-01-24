using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Provides context information to the password management system
    /// </summary>
    public class PasswordContext
    {
        /// <summary>
        /// Initializes a new instance of the PasswordContext class
        /// </summary>
        /// <param name="partition">The partition that the password operation is running against</param>
        internal PasswordContext(Partition partition)
        {
            this.Partition = partition;
        }

        /// <summary>
        /// Gets the partition that the password operation is running against
        /// </summary>
        public Partition Partition { get; }

        /// <summary>
        /// Gets or sets an object that can be used to store user-defined custom data that is shared by all password providers
        /// </summary>
        public object CustomData { get; set; }
    }
}
