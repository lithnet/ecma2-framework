using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface ISchemaProvider
    {
        Task GetConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions, int pageNumber);

        Task<ParameterValidationResult> ValidateConfigParametersAsync(IConfigParameters configParameters, int pageNumber);

        Task<Schema> GetMmsSchemaAsync(SchemaContext context);
    }
}
