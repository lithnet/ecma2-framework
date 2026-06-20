using System.Threading;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// A minimal <see cref="IObjectImportProvider"/> used in unit tests.
    /// All methods are no-ops or return trivially correct values.
    /// </summary>
    internal sealed class TestImportProvider : IObjectImportProvider
    {
        public Task InitializeAsync(ImportContext context)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CanImportAsync(SchemaType type)
        {
            return Task.FromResult(true);
        }

        public Task GetCSEntryChangesAsync(SchemaType type, ICSEntryChangeCollection csentryCollection, string incomingWatermark, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetOutboundWatermark(SchemaType type, CancellationToken cancellationToken)
        {
            return Task.FromResult<string>(null);
        }
    }
}
