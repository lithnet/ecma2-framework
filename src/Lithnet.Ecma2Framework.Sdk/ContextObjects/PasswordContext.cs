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
        /// Creates a <see cref="PasswordContext"/> from a worker-side partition.
        /// This factory is the intended entry point for the worker host; it allows the worker
        /// to create a password context without depending on internal constructor visibility.
        /// </summary>
        /// <param name="partition">The partition for this password operation. May be null.</param>
        /// <returns>A fully initialised <see cref="PasswordContext"/> ready for use by the orchestrator.</returns>
        public static PasswordContext Create(Partition partition)
        {
            return new PasswordContext(partition);
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
