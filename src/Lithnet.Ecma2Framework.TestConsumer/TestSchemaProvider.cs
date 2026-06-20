using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.TestConsumer
{
    public sealed class TestSchemaProvider : ISchemaProvider
    {
        public Task<Schema> GetMmsSchemaAsync()
        {
            Schema schema = Schema.Create();
            SchemaType userType = SchemaType.Create("user", false);
            userType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));
            userType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));
            schema.Types.Add(userType);
            return Task.FromResult(schema);
        }
    }
}
