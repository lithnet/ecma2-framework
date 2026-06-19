namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Exposes engine-supplied values that the management agent host (FIM/MIM) would normally read from the
    /// static <c>Microsoft.MetadirectoryServices.Utils</c> surface. Because providers run inside the
    /// out-of-process net8 worker — where the real <c>Utils</c> static resolves to the WORKER's own location —
    /// these values are captured engine-side by the net48 shim and injected into the worker on the connection
    /// handshake. Providers MUST read them through this service rather than the real static.
    /// </summary>
    public interface IEngineServices
    {
        /// <summary>
        /// Gets the host's extensions directory (the MIM <c>Extensions</c> folder), as reported by the engine's
        /// <c>Utils.ExtensionsDirectory</c> on the net48 side. Null when the value was not supplied by the host
        /// (for example, on the schema/capabilities/config-parameter paths that run before a connection is
        /// opened). The only engine-<c>Utils</c> value any current MA reads (ACMA and the SSH MA use it to
        /// locate a configuration file; the Okta MA uses none).
        /// </summary>
        string ExtensionsDirectory { get; }
    }
}
