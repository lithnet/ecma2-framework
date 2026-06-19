using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.SampleConsumer
{
    /// <summary>
    /// A minimal import provider. It exists only to satisfy the worker-role generator's discovery
    /// requirement (at least one of <see cref="IObjectImportProvider"/> / IObjectExportProvider must be
    /// present, otherwise ECMA2004 fails the worker build). The end-to-end test only calls GetSchema, so
    /// this provider returns an empty page.
    /// </summary>
    public sealed class SampleImportProvider : IObjectImportProvider
    {
        public Task InitializeAsync(ImportContext context)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CanImportAsync(SchemaType type)
        {
            return Task.FromResult(type.Name == "samplePerson");
        }

        public Task GetCSEntryChangesAsync(SchemaType type, ICSEntryChangeCollection csentryCollection, string incomingWatermark, CancellationToken cancellationToken)
        {
            // Empty page: the sample import provider produces no objects. It exists only to pass discovery.
            return Task.CompletedTask;
        }

        public Task<string> GetOutboundWatermark(SchemaType type, CancellationToken cancellationToken)
        {
            return Task.FromResult<string>(null);
        }
    }
}
