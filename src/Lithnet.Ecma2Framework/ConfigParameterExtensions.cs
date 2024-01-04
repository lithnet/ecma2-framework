using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public static class ConfigParameterExtensions
    {
        public static string GetParamString(this IConfigParameterContext context, string name)
        {
            return context.ConfigParameters.GetParamString(name);
        }

        public static int GetParamInt(this IConfigParameterContext context, string name)
        {
            return context.ConfigParameters.GetParamInt(name);
        }

        public static bool GetParamBool(this IConfigParameterContext context, string name)
        {
            return context.ConfigParameters.GetParamBool(name);
        }

        public static List<string> GetParamStrings(this IConfigParameterContext context, string name, char separator)
        {
            return context.ConfigParameters.GetParamStrings(name, separator);
        }

        public static string GetParamString(this KeyedCollection<string, ConfigParameter> parameters, string name)
        {
            if (!parameters.Contains(name))
            {
                throw new KeyNotFoundException($"A configuration parameter with the name {name} could not be found");
            }

            var v = parameters[name];

            if (v.IsEncrypted)
            {
                return v.SecureValue.ConvertToUnsecureString();
            }

            return v.Value;
        }

        public static List<string> GetParamStrings(this KeyedCollection<string, ConfigParameter> parameters, string name, char separator)
        {
            string value = GetParamString(parameters, name);

            if (string.IsNullOrWhiteSpace(name))
            {
                return new List<string>();
            }

            var values = value.Split(new[] { separator }, System.StringSplitOptions.RemoveEmptyEntries);

            return new List<string>(values.Select(t => t.Trim()));
        }

        public static int GetParamInt(this KeyedCollection<string, ConfigParameter> parameters, string name)
        {
            string value = parameters.GetParamString(name);

            if (value == null)
            {
                return 0;
            }

            return int.Parse(value);
        }

        public static bool GetParamBool(this KeyedCollection<string, ConfigParameter> parameters, string name)
        {
            string value = parameters.GetParamString(name);

            return value == "1";
        }
    }
}