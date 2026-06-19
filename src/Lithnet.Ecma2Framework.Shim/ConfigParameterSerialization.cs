using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Serialises a host config-parameter collection to the <see cref="MmsPipeSerializer"/> XML wire
    /// form (a serialised <see cref="List{T}"/> of real <see cref="ConfigParameter"/>). Shared by every
    /// shim connection that forwards the MA configuration parameters to the worker so the worker can
    /// build its DI container with the real parameter values.
    /// </summary>
    internal static class ConfigParameterSerialization
    {
        /// <summary>
        /// Serialises the supplied config-parameter collection to a <see cref="MmsPipeSerializer"/>
        /// XML string for transport to the worker.
        /// </summary>
        /// <param name="configParameters">The host config-parameter collection.</param>
        /// <returns>The serialised parameter list as an XML string.</returns>
        public static string Serialize(KeyedCollection<string, ConfigParameter> configParameters)
        {
            List<ConfigParameter> list = new List<ConfigParameter>(configParameters.Count);

            foreach (ConfigParameter parameter in configParameters)
            {
                list.Add(parameter);
            }

            return MmsPipeSerializer.SerializeXml<List<ConfigParameter>>(list);
        }
    }
}
