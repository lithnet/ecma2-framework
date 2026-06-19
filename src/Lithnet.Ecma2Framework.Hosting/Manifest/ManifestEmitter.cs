using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Hosting.Manifest
{
    /// <summary>
    /// Drives the worker's <c>--emit-manifest</c> mode: boots the consumer's DI container without a
    /// live connection, reads the real <see cref="MACapabilities"/> from the consumer's provider, and
    /// writes a FIM Packaged-MA manifest.
    /// <para>
    /// This type performs no IPC. It mirrors the provider-call path used by
    /// <see cref="SchemaRpcTarget"/>: capabilities come from <see cref="Ecma2InitOrchestrator.GetCapabilitiesAsync"/>,
    /// passed the empty <see cref="IConfigParameters"/> the container resolves at startup. Config parameters
    /// are NOT baked into the manifest — with omit-schema, FIM rediscovers them live via the shim's
    /// <c>IMAExtensible2GetParametersEx</c>, so the manifest's parameter sections are emitted empty.
    /// </para>
    /// <para>
    /// Error handling: the emitter fails loud. Any missing or blank required option throws before any
    /// provider runs; a null capabilities provider throws (capabilities are required for the manifest);
    /// and the manifest XML is fully built in memory before a single byte is written, so a failure
    /// never leaves a partial or blank file on disk.
    /// </para>
    /// </summary>
    internal static class ManifestEmitter
    {
        private const int InternalVersion = 1;

        /// <summary>
        /// Emits a Packaged-MA manifest to <see cref="ManifestEmitOptions.OutputPath"/> for the
        /// consumer hosted by <paramref name="host"/>.
        /// </summary>
        /// <param name="host">The worker host for the consumer assembly. Must be non-null.</param>
        /// <param name="options">The already-validated identity and output options. Required string values must be non-blank.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when any required option is null or blank.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the consumer registers no <see cref="ICapabilitiesProvider"/>.</exception>
        public static async Task EmitAsync(WorkerHost host, ManifestEmitOptions options)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            // Defensive validation: the MSBuild targets own the opt-in and validation, but this mode
            // still fails loud rather than ever writing a half/blank manifest from a missing input.
            RequireNonBlank(options.OutputPath, nameof(options.OutputPath));
            RequireNonBlank(options.ManagementAgentId, nameof(options.ManagementAgentId));
            RequireNonBlank(options.Company, nameof(options.Company));
            RequireNonBlank(options.ListName, nameof(options.ListName));
            RequireNonBlank(options.ShimFileName, nameof(options.ShimFileName));
            RequireNonBlank(options.AssemblyVersion, nameof(options.AssemblyVersion));

            host.BuildContainer(new EmptyConfigParameterCollection());

            ICapabilitiesProvider capabilitiesProvider = host.GetCapabilitiesProvider();

            if (capabilitiesProvider == null)
            {
                throw new InvalidOperationException(
                    "The consumer does not register an ICapabilitiesProvider. Capabilities are required to emit a manifest.");
            }

            // Resolve the empty IConfigParameters the container builds at startup — the same value the
            // RPC handlers and InitOrchestrator tests pass when no host-supplied parameters exist.
            IConfigParameters configParameters = host.Services.GetService<IConfigParameters>();

            Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(host.Services);

            MACapabilities capabilities = await orchestrator.GetCapabilitiesAsync(configParameters);

            if (capabilities == null)
            {
                throw new InvalidOperationException(
                    "The consumer's ICapabilitiesProvider returned null capabilities. Capabilities are required to emit a manifest.");
            }

            uint capabilityBits = CapabilityEncoder.GetCapabilityBits(capabilities);
            uint capabilitiesMask = CapabilityEncoder.GetCapabilitiesMask(capabilities);
            int exportType = CapabilityEncoder.GetExportType(capabilities);

            string helpText = string.IsNullOrWhiteSpace(options.HelpText) ? options.ListName : options.HelpText;

            ManifestIdentity identity = new ManifestIdentity
            {
                Company = options.Company,
                ListName = options.ListName,
                HelpText = helpText,
                ManagementAgentId = options.ManagementAgentId,
                InternalVersion = InternalVersion,
            };

            // Build the complete manifest string in memory first; only then touch the file system,
            // so a failure anywhere above never leaves a partial manifest on disk.
            string manifestXml = PackagedMaBuilder.Build(
                identity,
                capabilityBits,
                capabilitiesMask,
                exportType,
                options.ShimFileName,
                options.AssemblyVersion,
                capabilities);

            File.WriteAllText(options.OutputPath, manifestXml, new UTF8Encoding(false));
        }

        private static void RequireNonBlank(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(
                    string.Format("The required manifest option '{0}' is missing or blank.", name),
                    name);
            }
        }
    }
}
