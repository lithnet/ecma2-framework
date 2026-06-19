using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// A minimal <see cref="ISchemaProvider"/> used in unit tests.
    /// Returns a schema containing a single "Person" type with an "id" anchor attribute.
    /// </summary>
    internal sealed class TestSchemaProvider : ISchemaProvider
    {
        public Task<Schema> GetMmsSchemaAsync()
        {
            Schema schema = Schema.Create();
            SchemaType personType = SchemaType.Create("Person", false);
            personType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));
            personType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));
            schema.Types.Add(personType);
            return Task.FromResult(schema);
        }
    }
}
