namespace Lithnet.Ecma2Framework.Internal
{
    /// <summary>
    /// The default <see cref="IEngineServices"/> implementation. Registered as a singleton in the worker's DI
    /// container; the worker's RPC target sets <see cref="ExtensionsDirectory"/> from the value the net48 shim
    /// captures (engine-side) and injects on the connection-open handshake. The value is a constant for the run,
    /// so a single set on open suffices and providers read it for the lifetime of the container.
    /// </summary>
    internal sealed class EngineServices : IEngineServices
    {
        /// <inheritdoc/>
        public string ExtensionsDirectory { get; set; }
    }
}
