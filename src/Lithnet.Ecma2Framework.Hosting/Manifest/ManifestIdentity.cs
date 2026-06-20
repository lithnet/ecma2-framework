namespace Lithnet.Ecma2Framework.Hosting.Manifest
{
    /// <summary>
    /// Carries the identity and presentation values that the
    /// <see cref="PackagedMaBuilder"/> stamps into a Packaged-MA manifest. These
    /// values originate from the consumer-supplied customization information
    /// (company, list name, help text, a stable management-agent GUID, and the
    /// internal version), not from the running provider.
    /// </summary>
    internal sealed class ManifestIdentity
    {
        /// <summary>Gets or sets the company name (the manifest <c>&lt;ma-companyname&gt;</c>).</summary>
        public string Company { get; set; }

        /// <summary>Gets or sets the base list name. The manifest <c>&lt;ma-listname&gt;</c> is rendered as <c>"{ListName} ({Company})"</c>.</summary>
        public string ListName { get; set; }

        /// <summary>Gets or sets the help text shown in the management agent UI (the manifest <c>&lt;ma-help-text&gt;</c>).</summary>
        public string HelpText { get; set; }

        /// <summary>Gets or sets the stable management-agent GUID string (the manifest <c>&lt;id&gt;</c>), including braces.</summary>
        public string ManagementAgentId { get; set; }

        /// <summary>Gets or sets the internal version stamped into the manifest <c>&lt;internal-version&gt;</c>.</summary>
        public int InternalVersion { get; set; }
    }
}
