using System.Threading.Tasks;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.PackageTests
{
    /// <summary>
    /// A minimal schema provider. The package end-to-end test does not invoke the schema at runtime; it asserts
    /// only that the package alone drives the host exe + shim build. This provider exists to satisfy the
    /// generator's ISchemaProvider discovery requirement (ECMA2002).
    /// </summary>
    public sealed class PackageTestSchemaProvider : ISchemaProvider
    {
        public Task<Schema> GetMmsSchemaAsync()
        {
            Schema schema = Schema.Create();
            SchemaType personType = SchemaType.Create("packagePerson", false);
            personType.Attributes.Add(SchemaAttribute.CreateAnchorAttribute("id", AttributeType.String));
            personType.Attributes.Add(SchemaAttribute.CreateSingleValuedAttribute("displayName", AttributeType.String));
            schema.Types.Add(personType);
            return Task.FromResult(schema);
        }
    }
}
