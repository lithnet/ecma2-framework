using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real ConfigParameterDefinition. Six private backing fields drive everything
    // (m_name, m_parameterType, m_text, m_validation, m_defaultValue, m_userExtensible); the public typed
    // Create* factories collapse their inputs into these (array DropDown values -> comma-escaped validation
    // string, bool CheckBox default -> "1"/"0" string), so they cannot losslessly reproduce an arbitrary
    // six-field tuple (e.g. a String param with non-empty Text). Per host-to-model-mapping D4, GetObject
    // reconstructs via the private six-field ctor by reflection, carrying the raw fields verbatim.
    //
    // CheckBoxDefault is NOT carried — it is the computed getter ("1" == DefaultValue) and recomputes itself.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class ConfigParameterDefinitionSerializable
#else
    public class ConfigParameterDefinitionSerializable
#endif
    {
        private static readonly ConstructorInfo MasterCtor = typeof(ConfigParameterDefinition).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(ConfigParameterType), typeof(string), typeof(string), typeof(string), typeof(string), typeof(bool) },
            null);

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public ConfigParameterType Type { get; set; }

        [DataMember]
        public string Text { get; set; }

        [DataMember]
        public string Validation { get; set; }

        [DataMember]
        public string DefaultValue { get; set; }

        [DataMember]
        public bool DropDownExtensible { get; set; }

        internal ConfigParameterDefinitionSerializable(ConfigParameterDefinition definition)
        {
            this.SetObject(definition);
        }

        internal void SetObject(ConfigParameterDefinition definition)
        {
            this.Name = definition.Name;
            this.Type = definition.Type;
            this.Text = definition.Text;
            this.Validation = definition.Validation;
            this.DefaultValue = definition.DefaultValue;
            this.DropDownExtensible = definition.DropDownExtensible;
        }

        internal ConfigParameterDefinition GetObject()
        {
            // Private master ctor: (ConfigParameterType type, string name, string text, string validation,
            // string defaultValue, bool userExtensible). Reconstructs the exact six-field state losslessly.
            return (ConfigParameterDefinition)MasterCtor.Invoke(new object[]
            {
                this.Type,
                this.Name,
                this.Text,
                this.Validation,
                this.DefaultValue,
                this.DropDownExtensible,
            });
        }
    }
}
