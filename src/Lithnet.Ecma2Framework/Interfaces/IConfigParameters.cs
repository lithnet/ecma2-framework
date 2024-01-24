using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Represents a collection of configuration parameters provided by the Synchronization Service
    /// </summary>
    public interface IConfigParameters
    {
        /// <summary>
        /// Gets a boolean value from the configuration parameter set
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <param name="defaultValue">A value to provide if the key is not present or it's value is not present</param>
        /// <returns>The value of the configuration parameter, or the default value if it doesn't exist</returns>
        bool GetBool(string name, bool defaultValue);

        /// <summary>
        /// Gets an integer value from the configuration parameter set
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <param name="defaultValue">A value to provide if the key is not present or it's value is not present</param>
        /// <returns>The value of the configuration parameter, or the default value if it doesn't exist</returns>
        int GetInt(string name, int defaultValue);

        /// <summary>
        /// Gets a list of string values from the configuration parameter set
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <param name="separator">The character used a separator between string values</param>
        /// <returns>The value of the configuration parameter, or the default value if it doesn't exist</returns>
        List<string> GetList(string name, string separator);

        /// <summary>
        /// Gets a long integer value from the configuration parameter set
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <param name="defaultValue">A value to provide if the key is not present or it's value is not present</param>
        /// <returns>The value of the configuration parameter, or the default value if it doesn't exist</returns>
        long GetLong(string name, long defaultValue);

        /// <summary>
        /// Gets a string value from the configuration parameter set
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <returns>THe value of the configuration parameter, or null if the value doesn't exist</returns>
        public string GetString(string name);

        /// <summary>
        /// Gets a string value from the configuration parameter set
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <param name="defaultValue">A value to provide if the key is not present or it's value is not present</param>
        /// <returns>The value of the configuration parameter, or the default value if it doesn't exist</returns>
        public string GetString(string name, string defaultValue);

        /// <summary>
        /// Determines if a configuration parameter exists
        /// </summary>
        /// <param name="name">The name of the configuration parameter</param>
        /// <returns><see langword="true"/> if the parameter is present, <see langword="false"/>if it is not</returns>
        bool HasValue(string name);

        /// <summary>
        /// Gets the raw configuration parameter dictionary as provided by the synchronization service
        /// </summary>
        KeyedCollection<string, ConfigParameter> Parameters { get; }
    }
}
