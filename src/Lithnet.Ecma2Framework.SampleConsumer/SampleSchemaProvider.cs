using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.SampleConsumer
{
    /// <summary>
    /// Produces a distinctive, known schema so the end-to-end test can assert the exact shape that
    /// round-trips from this provider, through the worker, the pipe, and the marshalling layer, back to
    /// the FIM host via the generated shim. A single object type "samplePerson" with an anchor "id" and
    /// two single-valued attributes ("displayName", "email") gives unambiguous values to assert.
    /// </summary>
    public sealed class SampleSchemaProvider : ISchemaProvider
    {
        public Task<Schema> GetMmsSchemaAsync()
        {
            Schema schema = Schema.Create();
            SchemaType personType = SchemaType.Create("samplePerson", false);
            personType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));
            personType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));
            personType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("email", AttributeType.String));
            schema.Types.Add(personType);
            return Task.FromResult(schema);
        }
    }
}
