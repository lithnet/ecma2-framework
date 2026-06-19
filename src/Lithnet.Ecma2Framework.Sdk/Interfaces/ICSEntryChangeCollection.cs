using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// An object used to store CSEntryChange objects
    /// </summary>
    public interface ICSEntryChangeCollection
    {
        /// <summary>
        /// Adds the specified CSEntryChange object to the collection
        /// </summary>
        /// <param name="csentry">The CSEntryChange object to add to the collection</param>
        void AddCSEntryChange(CSEntryChange csentry);
    }
}
