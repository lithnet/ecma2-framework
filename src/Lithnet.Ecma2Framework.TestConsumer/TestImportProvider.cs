using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    public sealed class TestImportProvider : IObjectImportProvider
    {
        private readonly IConfigParameters configParameters;

        public TestImportProvider(IConfigParameters configParameters)
        {
            this.configParameters = configParameters;
        }

        public Task InitializeAsync(ImportContext context)
        {
            return Task.CompletedTask;
        }

        public Task<bool> CanImportAsync(SchemaType type)
        {
            return Task.FromResult(type.Name == "user");
        }

        public Task GetCSEntryChangesAsync(SchemaType type, ICSEntryChangeCollection csentryCollection, string incomingWatermark, CancellationToken cancellationToken)
        {
            // Add three fixed CSEntryChange objects for test/diagnostic purposes.
            for (int i = 1; i <= 3; i++)
            {
                CSEntryChange csentry = CSEntryChange.Create();
                csentry.ObjectModificationType = ObjectModificationType.Add;
                csentry.ObjectType = "user";
                csentry.AnchorAttributes.Add(AnchorAttribute.Create("id", "user-" + i));
                csentry.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("displayName", "Test User " + i));
                csentryCollection.AddCSEntryChange(csentry);
            }

            // Echo a config-parameter value back as an extra entry so an integration test can prove the
            // REAL configuration parameters reached the provider (i.e. crossed the pipe and built the
            // container). Only emitted when the parameter is supplied, so the default 3-entry tests are
            // unaffected.
            string echoValue = this.configParameters.GetString("importEchoValue", null);

            if (!string.IsNullOrEmpty(echoValue))
            {
                CSEntryChange echo = CSEntryChange.Create();
                echo.ObjectModificationType = ObjectModificationType.Add;
                echo.ObjectType = "user";
                echo.AnchorAttributes.Add(AnchorAttribute.Create("id", "echo"));
                echo.AttributeChanges.Add(AttributeChange.CreateAttributeAdd("displayName", echoValue));
                csentryCollection.AddCSEntryChange(echo);
            }

            return Task.CompletedTask;
        }

        public Task<string> GetOutboundWatermark(SchemaType type, CancellationToken cancellationToken)
        {
            return Task.FromResult("test-watermark-v1");
        }
    }
}
