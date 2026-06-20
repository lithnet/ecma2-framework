using System.Globalization;
using System.Xml.Linq;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.Manifest
{
    /// <summary>
    /// Assembles a complete FIM <c>format="Packaged"</c> management-agent manifest
    /// from already-computed inputs. This is a pure XML transform: it performs no
    /// IPC, runs no provider, and reads no files.
    /// <para>
    /// The structure is pinned byte-for-byte against a real manifest produced by FIM's
    /// <c>mapackager</c> tool (the committed golden reference in the Worker.Tests project,
    /// <c>Manifest\packaged.golden.xml</c>, from a probe MA packaged with <c>omit-schema</c>).
    /// The deliberate, ground-truth differences from a vanilla <c>mapackager</c> export are:
    /// </para>
    /// <list type="bullet">
    /// <item>The DSML <c>&lt;schema&gt;</c> is omitted; FIM re-discovers it at config time via the
    /// <c>&lt;rediscover-schema mode="dsml"&gt;</c> directive, which is the FIRST child of
    /// <c>private-configuration/MAConfig/ui-data</c> (NOT at the <c>ma-data</c> level).</item>
    /// <item><c>&lt;parameter-definitions&gt;</c> and <c>&lt;parameter-values&gt;</c> are emitted EMPTY.
    /// With <c>omit-schema</c> FIM rediscovers the config parameters live from the extension's
    /// <c>GetParametersEx</c>, so no parameter rows are baked into the manifest.</item>
    /// <item><c>&lt;supports-parameters-ex&gt;</c> is <c>1</c>; the v3 shim implements
    /// <c>IMAExtensible2GetParametersEx</c>.</item>
    /// <item><c>&lt;capabilities-mask&gt;</c> IS emitted at the <c>ma-data</c> level (after
    /// <c>&lt;ma-run-data&gt;</c>) in lowercase hex with no <c>0x</c> prefix. It is NOT identical to the
    /// decimal <c>&lt;capability-bits&gt;</c> in <c>extension-config</c>: for an export-capable MA the
    /// engine additionally stamps the <c>ImmediateExportConfirmation</c> (0x1) bit into the mask only.
    /// The caller passes the already-computed mask (see <c>CapabilityEncoder.GetCapabilitiesMask</c>).</item>
    /// </list>
    /// </summary>
    internal static class PackagedMaBuilder
    {
        // Page sizes reported by the engine for an import/export run. These mirror the v3 SHIM's
        // connection values (Lithnet.Ecma2Framework.Shim ImportConnection / ExportConnection):
        //   ImportConnection.ImportDefaultPageSize = 100,  ImportConnection.ImportMaxPageSize = 10000
        //   ExportConnection.ExportDefaultPageSize = 100,  ExportConnection.ExportMaxPageSize = 10000
        // The worker cannot reference the net48 shim, so these are hard-coded here. Keep them in
        // lockstep with the shim if the shim's page sizes ever change.
        private const string ImportDefaultPageSize = "100";
        private const string ImportMaxPageSize = "10000";
        private const string ExportDefaultPageSize = "100";
        private const string ExportMaxPageSize = "10000";

        /// <summary>
        /// Builds the Packaged-MA manifest XML string.
        /// </summary>
        /// <param name="identity">Identity and presentation values (company, list name, help text, GUID, internal version).</param>
        /// <param name="capabilityBits">The encoded capability bits, written to <c>&lt;capability-bits&gt;</c> (decimal) in extension-config.</param>
        /// <param name="capabilitiesMask">The encoded capabilities mask, written to <c>&lt;capabilities-mask&gt;</c> (lowercase hex) at the ma-data level. For an export-capable MA this is <paramref name="capabilityBits"/> with the mask-only ImmediateExportConfirmation (0x1) bit set.</param>
        /// <param name="exportType">The raw <c>MAExportType</c> value, written to both <c>&lt;export-type&gt;</c> locations.</param>
        /// <param name="shimFileName">The shim assembly file name written to <c>&lt;filename&gt;</c>.</param>
        /// <param name="assemblyVersion">The shim assembly version written to <c>&lt;assembly-version&gt;</c>.</param>
        /// <param name="capabilities">The capabilities used to derive import/export/password enablement flags.</param>
        /// <returns>The complete manifest as an XML string.</returns>
        public static string Build(ManifestIdentity identity, uint capabilityBits, uint capabilitiesMask, int exportType, string shimFileName, string assemblyVersion, MACapabilities capabilities)
        {
            string exportTypeText = exportType.ToString(CultureInfo.InvariantCulture);

            // <capabilities-mask> is the engine capabilities mask in lowercase hex with no 0x prefix
            // (e.g. "810780b8" import-only, "81079839" export). It differs from the decimal
            // <capability-bits> by the mask-only ImmediateExportConfirmation bit on export-capable MAs.
            string capabilitiesMaskText = capabilitiesMask.ToString("x", CultureInfo.InvariantCulture);

            XElement maData = new XElement("ma-data",
                new XAttribute("format", "Packaged"),
                new XElement("format-version", "1"),
                new XElement("internal-version", identity.InternalVersion.ToString(CultureInfo.InvariantCulture)),
                new XElement("ma-companyname", identity.Company),
                // The subtype literal is intentionally hard-coded to "ecma2-framework"; only the company varies.
                new XElement("subtype", string.Format(CultureInfo.InvariantCulture, "ecma2-framework ({0})", identity.Company)),
                new XElement("ma-listname", string.Format(CultureInfo.InvariantCulture, "{0} ({1})", identity.ListName, identity.Company)),
                new XElement("id", identity.ManagementAgentId),
                new XElement("category", "Extensible2"),
                // password-sync-allowed defaults to 0 to match mapackager output. MIM password SYNC is a
                // separate administrator opt-in and is NOT the same as the caps SupportPassword capability,
                // which governs whether password-management is supported at all (emitted further below).
                new XElement("password-sync-allowed", "0"),
                // MIM's packaged-MA import REQUIRES a DSML <schema> here in <ma-data> - it rejects an MA with
                // none (our earlier <rediscover-schema> directive was wrong). It does NOT need the complete
                // schema: an ECMA2 connector's schema is dynamic (discovered per-tenant against live
                // connectivity), so no packaged MA can carry it in advance. We emit a minimal valid PLACEHOLDER
                // (one structural class + one anchor attribute); after import the admin configures connectivity
                // and refreshes the schema, replacing this with the real live-discovered schema. See
                // BuildPlaceholderSchema.
                BuildPlaceholderSchema(),
                new XElement("attribute-inclusion"),
                new XElement("stay-disconnector"),
                new XElement("join"),
                new XElement("projection"),
                new XElement("export-attribute-flow"),
                new XElement("extension"),
                new XElement("controller-configuration",
                    new XElement("application-architecture", "process")),
                new XElement("ma-ui-settings",
                    new XElement("account-joiner-queries",
                        new XElement("attributes"),
                        new XElement("filters",
                            new XAttribute("max_mv_search_results", "")))),
                BuildPrivateConfiguration(identity, capabilityBits, exportTypeText, shimFileName, assemblyVersion, capabilities),
                new XElement("SyncConfig-refresh-schema", "0"),
                new XElement("ma-partition-data"),
                new XElement("ma-run-data"),
                // <capabilities-mask> is the engine mask (bits + the export-only ImmediateExportConfirmation
                // bit), rendered as lowercase hex.
                new XElement("capabilities-mask", capabilitiesMaskText),
                new XElement("export-type", exportTypeText),
                new XElement("dn-construction"),
                new XElement("password-sync",
                    new XElement("maximum-retry-count", "10"),
                    new XElement("retry-interval", "60"),
                    new XElement("allow-low-security", "0")),
                new XElement("component_mappings"));

            XElement exportMa = new XElement("export-ma",
                maData,
                new XElement("mv-data",
                    new XElement("import-attribute-flow")));

            XDocument document = new XDocument(exportMa);
            return document.ToString();
        }

        private static XElement BuildPrivateConfiguration(ManifestIdentity identity, uint capabilityBits, string exportTypeText, string shimFileName, string assemblyVersion, MACapabilities capabilities)
        {
            return new XElement("private-configuration",
                new XElement("MAConfig",
                    new XElement("ui-data",
                        // <ui-data> carries only the import-wizard config; the schema lives in <ma-data>
                        // (BuildPlaceholderSchema), matching a real packaged MA. There is no <rediscover-schema>.
                        new XElement("xmlwizard",
                            new XElement("properties",
                                new XElement("sample_file"),
                                new XElement("code_page_description")),
                            new XElement("partitions"),
                            new XElement("primary_class_mappings"),
                            new XElement("object_classes"),
                            new XElement("attributes")),
                        new XElement("ma-help-text", identity.HelpText)),
                    new XElement("importing"),
                    new XElement("exporting"),
                    new XElement("ldap-dn", "0"),
                    new XElement("change_type_attribute"),
                    new XElement("add_change_type_value", "Add"),
                    new XElement("modify_change_type_value", "Modify"),
                    new XElement("delete_change_type_value", "Delete"),
                    new XElement("primary_class_mappings"),
                    new XElement("enable-unapplied-merge", "0"),
                    new XElement("file-type", "Extensible2"),
                    BuildExtensionConfig(capabilityBits, exportTypeText, shimFileName, assemblyVersion, capabilities),
                    // parameter-definitions and parameter-values are emitted EMPTY: with omit-schema, FIM
                    // rediscovers the config parameters live via the shim's IMAExtensible2GetParametersEx.
                    new XElement("parameter-definitions"),
                    new XElement("parameter-values"),
                    // default_visible_attributes references schema attributes we do not bake; kept but empty.
                    // (The real probe export carried id/displayName from its export schema; our schema-less
                    // generation legitimately differs here.)
                    new XElement("default_visible_attributes"),
                    new XElement("password-extension-config",
                        new XElement("password-extension-enabled", "0"),
                        new XElement("dll", new XAttribute("data-owner", "ISV")),
                        new XElement("password-set-enabled"),
                        new XElement("password-change-enabled"),
                        new XElement("connection-info",
                            new XElement("connect-to"),
                            new XElement("user")),
                        new XElement("timeout")),
                    new XElement("case_normalize_dn_for_anchor", "1")));
        }

        /// <summary>
        /// Builds the minimal DSML <c>&lt;schema&gt;</c> that MIM requires in a packaged MA's <c>&lt;ma-data&gt;</c>.
        /// MIM's import rejects a packaged MA with no schema, but it does NOT need the real schema: an ECMA2
        /// connector's schema is dynamic (discovered per-tenant against live connectivity), so no packaged MA can
        /// carry the complete schema in advance. This emits a single structural class with a single required
        /// anchor attribute - the smallest valid schema that satisfies import. After import the administrator
        /// configures connectivity and refreshes the schema, at which point the MA's real, live-discovered schema
        /// replaces this placeholder. The shape (namespaces, attribute-type with a DirectoryString syntax OID,
        /// anchor flagging) mirrors the schema block of a real, MIM-accepted packaged MA.
        /// </summary>
        private static XElement BuildPlaceholderSchema()
        {
            XNamespace dsml = "http://www.dsml.org/DSML";
            XNamespace msDsml = "http://www.microsoft.com/MMS/DSML";

            return new XElement("schema",
                new XElement(dsml + "dsml",
                    new XAttribute(XNamespace.Xmlns + "ms-dsml", msDsml.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "dsml", dsml.NamespaceName),
                    new XElement(dsml + "directory-schema",
                        new XAttribute(msDsml + "no-objectclass-validation", "true"),
                        new XElement(dsml + "class",
                            new XAttribute("id", "placeholder"),
                            new XAttribute("type", "structural"),
                            new XAttribute(msDsml + "locked", "1"),
                            new XElement(dsml + "name", "placeholder"),
                            new XElement(dsml + "attribute",
                                new XAttribute("ref", "#id"),
                                new XAttribute("required", "true"),
                                new XAttribute(msDsml + "isAnchor", "true"),
                                new XAttribute(msDsml + "allowedOperation", "ImportOnly"))),
                        new XElement(dsml + "attribute-type",
                            new XAttribute("id", "id"),
                            new XAttribute("single-value", "true"),
                            new XElement(dsml + "name", "id"),
                            // 1.3.6.1.4.1.1466.115.121.1.15 = DirectoryString, the syntax a real packaged MA uses.
                            new XElement(dsml + "syntax", "1.3.6.1.4.1.1466.115.121.1.15")))));
        }

        private static XElement BuildExtensionConfig(uint capabilityBits, string exportTypeText, string shimFileName, string assemblyVersion, MACapabilities capabilities)
        {
            return new XElement("extension-config",
                new XElement("filename", new XAttribute("data-owner", "ISV"), shimFileName),
                new XElement("import-default-page-size", ImportDefaultPageSize),
                new XElement("import-max-page-size", ImportMaxPageSize),
                new XElement("export-default-page-size", ExportDefaultPageSize),
                new XElement("export-max-page-size", ExportMaxPageSize),
                // export-mode is "call-based" when export is supported, and EMPTY when it is not (matching
                // mapackager output for an import-only MA). import-mode is always "call-based".
                new XElement("export-mode", new XAttribute("data-owner", "ISV"), ExportMode(capabilities.SupportExport)),
                new XElement("import-mode", "call-based"),
                new XElement("export-enabled", new XAttribute("data-owner", "ISV"), BoolToBit(capabilities.SupportExport)),
                new XElement("import-enabled", new XAttribute("data-owner", "ISV"), BoolToBit(capabilities.SupportImport)),
                new XElement("discovery-partition"),
                new XElement("discovery-schema", "extensibility"),
                new XElement("discovery-hierarchy"),
                // password-management-enabled is "1" when password is supported, and EMPTY when it is not
                // (matching mapackager output; confirmed "1" for a password-capable MA from the shipped Okta manifest).
                new XElement("password-management-enabled", PasswordManagementEnabled(capabilities.SupportPassword)),
                new XElement("assembly-version", assemblyVersion),
                // supports-parameters-ex is 1: the v3 shim implements IMAExtensible2GetParametersEx.
                new XElement("supports-parameters-ex", "1"),
                new XElement("export-type", exportTypeText),
                new XElement("capability-bits", capabilityBits.ToString(CultureInfo.InvariantCulture)));
        }

        private static string ExportMode(bool supportExport)
        {
            if (supportExport)
            {
                return "call-based";
            }

            return string.Empty;
        }

        private static string PasswordManagementEnabled(bool supportPassword)
        {
            if (supportPassword)
            {
                return "1";
            }

            return string.Empty;
        }

        private static string BoolToBit(bool value)
        {
            if (value)
            {
                return "1";
            }

            return "0";
        }
    }
}
