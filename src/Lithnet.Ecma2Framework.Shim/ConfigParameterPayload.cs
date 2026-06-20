using System.Collections.Generic;
using System.Collections.ObjectModel;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Serialises the host config-parameter collection into the <c>MmsPipeSerializer</c> XML payload
    /// (a <c>List&lt;ConfigParameter&gt;</c>) the worker expects on the capabilities and config-parameter
    /// RPC calls. The real <see cref="ConfigParameter"/> objects cross verbatim via the shared surrogate
    /// (including encrypted SecureValue content), so no per-field translation occurs.
    /// </summary>
    internal static class ConfigParameterPayload
    {
        /// <summary>
        /// Serialises the supplied collection to a <c>List&lt;ConfigParameter&gt;</c> XML string.
        /// A null collection yields the serialisation of an empty list.
        /// </summary>
        public static string Serialize(KeyedCollection<string, ConfigParameter> configParameters)
        {
            List<ConfigParameter> parameters = new List<ConfigParameter>();

            if (configParameters != null)
            {
                foreach (ConfigParameter parameter in configParameters)
                {
                    parameters.Add(parameter);
                }
            }

            return MmsPipeSerializer.SerializeXml<List<ConfigParameter>>(parameters);
        }
    }
}
