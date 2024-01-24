using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// A default implementation of the IConfigParameters provider, which wraps the native configuration parameters provided by the synchronization service
    /// </summary>
    public class ConfigParameters : IConfigParameters
    {
        private KeyedCollection<string, ConfigParameter> parameters;

        internal event EventHandler ConfigParametersChanged;

        /// <summary>
        /// Initializes a new instance of the ConfigParameters class
        /// </summary>
        public ConfigParameters()
        {
        }

        /// <summary>
        /// Initializes a new instance of the ConfigParameters class
        /// </summary>
        /// <param name="parameters">The config parameter dictionary provided by the synchronization service</param>
        public ConfigParameters(KeyedCollection<string, ConfigParameter> parameters)
        {
            this.Parameters = parameters;
        }

        /// <inheritdoc/>
        public KeyedCollection<string, ConfigParameter> Parameters
        {
            get { return this.parameters; }
            internal set
            {
                this.parameters = value;
                this.ConfigParametersChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        public bool HasValue(string name)
        {
            if (this.Parameters == null)
            {
                return false;
            }

            return this.Parameters.Contains(name);
        }

        /// <inheritdoc/>
        public string GetString(string name)
        {
            return this.GetString(name, null);
        }

        /// <inheritdoc/>
        public string GetString(string name, string defaultValue)
        {
            if (this.Parameters == null)
            {
                return defaultValue;
            }

            if (this.Parameters.Contains(name))
            {
                if (this.Parameters[name].IsEncrypted)
                {
                    return this.Parameters[name].SecureValue?.ConvertToUnsecureString();
                }
                else
                {
                    return this.Parameters[name].Value;
                }
            }
            else
            {
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public bool GetBool(string name, bool defaultValue)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (bool.TryParse(value, out bool result))
            {
                return result;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public int GetInt(string name, int defaultValue)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (int.TryParse(value, out int result))
            {
                return result;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public long GetLong(string name, long defaultValue)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            if (long.TryParse(value, out long result))
            {
                return result;
            }
            else
            {
                return defaultValue;
            }
        }

        /// <inheritdoc/>
        public List<string> GetList(string name, string separator)
        {
            string value = this.GetString(name);

            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<string>();
            }

            return new List<string>(value.Split(new string[] { separator }, System.StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
