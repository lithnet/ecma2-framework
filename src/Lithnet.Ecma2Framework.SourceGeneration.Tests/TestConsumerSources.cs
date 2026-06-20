namespace Lithnet.Ecma2Framework.SourceGeneration.Tests
{
    /// <summary>
    /// Shared consumer source fixtures for the generator tests. The minimal valid consumer declares everything
    /// discovery requires to emit without error - an IEcmaStartup, a schema provider, a capabilities provider, and at
    /// least one object provider - in a caller-chosen namespace. The referenced-assembly discovery tests need the
    /// participant types to be PUBLIC (the generated host is a separate assembly), so every type here is public except
    /// where a fixture deliberately makes the startup non-public to exercise the ECMA2013 guard.
    /// </summary>
    internal static class TestConsumerSources
    {
        /// <summary>
        /// Returns a minimal but complete consumer source in namespace <paramref name="ns"/>. The startup is named
        /// <c>DemoStartup</c> and is public, as are the schema, capabilities, and import providers, so the source is
        /// usable both for the in-source discovery path and for the cross-assembly referenced-consumer path.
        /// </summary>
        public static string MinimalValidConsumer(string ns)
        {
            return BuildConsumer(ns, "public", "public");
        }

        /// <summary>
        /// Returns a consumer whose startup is public but whose providers are <c>internal</c> - the real-world
        /// shape (e.g. the Okta MA), where only the startup and option classes are made public and the provider
        /// implementations stay internal because the generated host code never names them (it resolves them via
        /// DI). The generator must still DISCOVER the internal providers so the worker-role existence checks
        /// (ECMA2002-2005) pass and the worker Main is emitted without error.
        /// </summary>
        public static string MinimalConsumerWithInternalProviders(string ns)
        {
            return BuildConsumer(ns, "public", "internal");
        }

        /// <summary>
        /// Returns the same minimal consumer as <see cref="MinimalValidConsumer(string)"/>, except the
        /// <c>DemoStartup</c> class is <c>internal</c>. The ONLY difference from the valid fixture is the startup's
        /// accessibility, isolating the ECMA2013 "participant must be public" path in the referenced-assembly
        /// discovery flow.
        /// </summary>
        public static string MinimalConsumerWithInternalStartup(string ns)
        {
            return BuildConsumer(ns, "internal", "public");
        }

        /// <summary>
        /// Returns the same minimal consumer as <see cref="MinimalValidConsumer(string)"/>, except the
        /// <c>DemoStartup</c> class is itself <c>public</c> but is nested inside an <c>internal</c> outer type. The
        /// startup's OWN modifier passes a naive public check, yet the type is NOT externally accessible from a
        /// separate host assembly, so the generator must still flag ECMA2013 on effective accessibility. Every other
        /// required participant is public and top-level, isolating the nested-startup case.
        /// </summary>
        public static string MinimalConsumerWithNestedPublicStartup(string ns)
        {
            return $@"
namespace {ns}
{{
    internal static class Outer
    {{
        public class DemoStartup : Lithnet.Ecma2Framework.IEcmaStartup
        {{
            public void Configure(Microsoft.Extensions.Configuration.IConfigurationBuilder builder) {{ }}
            public void SetupServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Lithnet.Ecma2Framework.IConfigParameters configParameters) {{ }}
        }}
    }}

    public class DemoSchemaProvider : Lithnet.Ecma2Framework.ISchemaProvider
    {{
        public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.Schema> GetMmsSchemaAsync() => throw new System.NotImplementedException();
    }}

    public class DemoCapabilitiesProvider : Lithnet.Ecma2Framework.ICapabilitiesProvider
    {{
        public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.MACapabilities> GetCapabilitiesAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    }}

    public class DemoImportProvider : Lithnet.Ecma2Framework.IObjectImportProvider
    {{
        public System.Threading.Tasks.Task InitializeAsync(Lithnet.Ecma2Framework.ImportContext context) => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<bool> CanImportAsync(Microsoft.MetadirectoryServices.SchemaType type) => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task GetCSEntryChangesAsync(Microsoft.MetadirectoryServices.SchemaType type, Lithnet.Ecma2Framework.ICSEntryChangeCollection csentryCollection, string incomingWatermark, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<string> GetOutboundWatermark(Microsoft.MetadirectoryServices.SchemaType type, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }}
}}
";
        }

        private static string BuildConsumer(string ns, string startupAccessibility, string providerAccessibility)
        {
            return $@"
namespace {ns}
{{
    {startupAccessibility} class DemoStartup : Lithnet.Ecma2Framework.IEcmaStartup
    {{
        public void Configure(Microsoft.Extensions.Configuration.IConfigurationBuilder builder) {{ }}
        public void SetupServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services, Lithnet.Ecma2Framework.IConfigParameters configParameters) {{ }}
    }}

    {providerAccessibility} class DemoSchemaProvider : Lithnet.Ecma2Framework.ISchemaProvider
    {{
        public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.Schema> GetMmsSchemaAsync() => throw new System.NotImplementedException();
    }}

    {providerAccessibility} class DemoCapabilitiesProvider : Lithnet.Ecma2Framework.ICapabilitiesProvider
    {{
        public System.Threading.Tasks.Task<Microsoft.MetadirectoryServices.MACapabilities> GetCapabilitiesAsync(Lithnet.Ecma2Framework.IConfigParameters configParameters) => throw new System.NotImplementedException();
    }}

    {providerAccessibility} class DemoImportProvider : Lithnet.Ecma2Framework.IObjectImportProvider
    {{
        public System.Threading.Tasks.Task InitializeAsync(Lithnet.Ecma2Framework.ImportContext context) => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<bool> CanImportAsync(Microsoft.MetadirectoryServices.SchemaType type) => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task GetCSEntryChangesAsync(Microsoft.MetadirectoryServices.SchemaType type, Lithnet.Ecma2Framework.ICSEntryChangeCollection csentryCollection, string incomingWatermark, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
        public System.Threading.Tasks.Task<string> GetOutboundWatermark(Microsoft.MetadirectoryServices.SchemaType type, System.Threading.CancellationToken cancellationToken) => throw new System.NotImplementedException();
    }}
}}
";
        }
    }
}
