using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Carries the real ConfigParameter. The host getters guard against the wrong accessor (Value throws for a
    // SecureString-encrypted param; SecureValue throws when not encrypted/secure), so SetObject reads the raw
    // backing fields by reflection instead of the public getters, and carries the two internal legality flags
    // (isEncrypted, usesSecureString). GetObject selects the matching ctor from those flags.
    //
    // SecureValue: the encrypted secret is carried as its unsecured string content (SecureValueContent) and a
    // SecureString is reconstructed in GetObject. This is the only DataContract-transportable form of a
    // SecureString. Production-gated behind same-identity pipe verification, but the DTO carries it so the
    // round-trip preserves the secret.
    [DataContract]
#if ECMA2_SHIM_INTERNAL
    internal class ConfigParameterSerializable
#else
    public class ConfigParameterSerializable
#endif
    {
        private static readonly FieldInfo ValueField =
            typeof(ConfigParameter).GetField("value", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo SecureValueField =
            typeof(ConfigParameter).GetField("secureValue", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo IsEncryptedField =
            typeof(ConfigParameter).GetField("isEncrypted", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo UsesSecureStringField =
            typeof(ConfigParameter).GetField("usesSecureString", BindingFlags.Instance | BindingFlags.NonPublic);

        [DataMember]
        public string Name { get; set; }

        [DataMember]
        public string Value { get; set; }

        [DataMember]
        public bool IsEncrypted { get; set; }

        // Internal host flag, load-bearing for ctor selection: distinguishes a SecureString-backed encrypted
        // param from an ECMA1-style (string-backed) encrypted param. Without it GetObject cannot pick the
        // correct ctor and the host getter guards mis-fire.
        [DataMember]
        public bool UsesSecureString { get; set; }

        // The SecureValue secret, carried as its plaintext content. Reconstructed to a SecureString in
        // GetObject. Crosses only over an identity-verified pipe in production.
        [DataMember]
        public string SecureValueContent { get; set; }

        internal ConfigParameterSerializable(ConfigParameter parameter)
        {
            this.SetObject(parameter);
        }

        internal void SetObject(ConfigParameter parameter)
        {
            this.Name = parameter.Name;
            this.IsEncrypted = (bool)IsEncryptedField.GetValue(parameter);
            this.UsesSecureString = (bool)UsesSecureStringField.GetValue(parameter);

            // Read the raw value backing field directly; the public Value getter throws for a
            // SecureString-encrypted param.
            this.Value = (string)ValueField.GetValue(parameter);

            SecureString secure = (SecureString)SecureValueField.GetValue(parameter);
            if (secure != null)
            {
                this.SecureValueContent = ConvertToUnsecureString(secure);
            }
        }

        internal ConfigParameter GetObject()
        {
            if (this.UsesSecureString)
            {
                SecureString secure = ConvertToSecureString(this.SecureValueContent);
                return new ConfigParameter(this.Name, secure);
            }

            if (this.IsEncrypted)
            {
                return new ConfigParameter(this.Name, this.Value, true);
            }

            return new ConfigParameter(this.Name, this.Value);
        }

        private static string ConvertToUnsecureString(SecureString secureString)
        {
            IntPtr unmanaged = IntPtr.Zero;
            try
            {
                unmanaged = System.Runtime.InteropServices.Marshal.SecureStringToGlobalAllocUnicode(secureString);
                return System.Runtime.InteropServices.Marshal.PtrToStringUni(unmanaged);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeGlobalAllocUnicode(unmanaged);
            }
        }

        private static SecureString ConvertToSecureString(string value)
        {
            SecureString secure = new SecureString();

            if (value != null)
            {
                foreach (char c in value)
                {
                    secure.AppendChar(c);
                }
            }

            secure.MakeReadOnly();
            return secure;
        }
    }
}
