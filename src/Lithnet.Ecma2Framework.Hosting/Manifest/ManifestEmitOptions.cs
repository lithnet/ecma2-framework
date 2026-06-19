namespace Lithnet.Ecma2Framework.Hosting.Manifest
{
    /// <summary>
    /// Carries the already-validated command-line inputs for the worker's <c>--emit-manifest</c>
    /// mode. The MA identity values (company, list name, help text, GUID) originate from the
    /// MSBuild targets that own the opt-in and validation; this carrier simply transports them
    /// to <see cref="ManifestEmitter"/>, which still fails loudly if a required value is missing.
    /// </summary>
    internal sealed class ManifestEmitOptions
    {
        /// <summary>Gets or sets the absolute path the manifest XML is written to.</summary>
        public string OutputPath { get; set; }

        /// <summary>Gets or sets the stable management-agent GUID string (the manifest <c>&lt;id&gt;</c>).</summary>
        public string ManagementAgentId { get; set; }

        /// <summary>Gets or sets the company name (the manifest <c>&lt;ma-companyname&gt;</c>).</summary>
        public string Company { get; set; }

        /// <summary>Gets or sets the base list name (the manifest <c>&lt;ma-listname&gt;</c> stem).</summary>
        public string ListName { get; set; }

        /// <summary>Gets or sets the help text shown in the management agent UI. May be blank, in which case it defaults to <see cref="ListName"/>.</summary>
        public string HelpText { get; set; }

        /// <summary>Gets or sets the shim assembly file name written to <c>&lt;filename&gt;</c>.</summary>
        public string ShimFileName { get; set; }

        /// <summary>Gets or sets the shim assembly version written to <c>&lt;assembly-version&gt;</c>.</summary>
        public string AssemblyVersion { get; set; }
    }
}
