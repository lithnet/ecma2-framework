using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.PasswordIdentity
{
    /// <summary>
    /// A <see cref="ManagementAgent"/> exposing only the MA name carried on the password-path identity.
    /// The DN-construction and normalisation helpers are engine-bound and fail loud.
    /// </summary>
    internal sealed class DetachedManagementAgent : ManagementAgent
    {
        private readonly string name;

        public DetachedManagementAgent(string name)
        {
            this.name = name;
        }

        public override string Name
        {
            get { return this.name; }
        }

        public override ReferenceValue CreateDN(string dn)
        {
            throw PasswordIdentityNotSupported.Member("ManagementAgent.CreateDN");
        }

        public override ReferenceValue CreateDN(Value dn)
        {
            throw PasswordIdentityNotSupported.Member("ManagementAgent.CreateDN");
        }

        public override ReferenceValue EscapeDNComponent(string[] parts)
        {
            throw PasswordIdentityNotSupported.Member("ManagementAgent.EscapeDNComponent");
        }

        public override ReferenceValue EscapeDNComponent(Value[] parts)
        {
            throw PasswordIdentityNotSupported.Member("ManagementAgent.EscapeDNComponent");
        }

        public override string NormalizeString(string value)
        {
            throw PasswordIdentityNotSupported.Member("ManagementAgent.NormalizeString");
        }

        public override string[] UnescapeDNComponent(string component)
        {
            throw PasswordIdentityNotSupported.Member("ManagementAgent.UnescapeDNComponent");
        }
    }
}
