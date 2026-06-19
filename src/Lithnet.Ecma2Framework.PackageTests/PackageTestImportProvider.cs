using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.PackageTests
{
    /// <summary>
    /// A minimal import provider. It exists only to satisfy the worker-role generator's discovery requirement
    /// (at least one of <see cref="IObjectImportProvider"/> / IObjectExportProvider must be present, otherwise
    /// ECMA2004 fails the host build).
    /// </summary>
    public sealed class PackageTestImportProvider : IObjectImportProvider
    {
        public Task InitializeAsync(ImportContext context)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CanImportAsync(SchemaType type)
        {
            return Task.FromResult(type.Name == "packagePerson");
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
