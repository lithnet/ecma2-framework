using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    public sealed class TestExportProvider : IObjectExportProvider
    {
        public Task InitializeAsync(ExportContext context)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CanExportAsync(CSEntryChange csentry)
        {
            return Task.FromResult(true);
        }

        public Task<CSEntryChangeResult> PutCSEntryChangeAsync(CSEntryChange csentry, CancellationToken cancellationToken)
        {
            if (csentry.ObjectType == "failme")
            {
                return Task.FromResult(CSEntryChangeResult.Create(
                    csentry.Identifier,
                    new List<AttributeChange>(),
                    MAExportError.ExportErrorConnectedDirectoryError,
                    "test-export-error",
                    "TestExportProvider was told to fail this object"));
            }

            return Task.FromResult(CSEntryChangeResult.Create(
                csentry.Identifier,
                new List<AttributeChange>(),
                MAExportError.Success));
        }
    }
}
