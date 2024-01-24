using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Defines the methods and properties that a schema provider must implement
    /// </summary>
    public interface ISchemaProvider
    {
        /// <summary>
        /// Gets the management agent's schema
        /// </summary>
        /// <returns>A Schema object representing the objects and attributes used by the management agent</returns>
        Task<Schema> GetMmsSchemaAsync();
    }
}
