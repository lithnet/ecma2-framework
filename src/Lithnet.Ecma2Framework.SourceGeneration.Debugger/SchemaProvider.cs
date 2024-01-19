using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.SourceGeneration.Debugger
{
    internal class SchemaProvider : ISchemaProvider
    {
        public Task GetConfigParametersAsync(IConfigParameters existingParameters, IList<ConfigParameterDefinition> newDefinitions, int pageNumber)
        {
            throw new NotImplementedException();
        }

        public Task<Schema> GetMmsSchemaAsync()
        {
            throw new NotImplementedException();
        }

        public Task<ParameterValidationResult> ValidateConfigParametersAsync(IConfigParameters configParameters, int pageNumber)
        {
            throw new NotImplementedException();
        }
    }

    internal class ImportProvider : IObjectImportProvider
    {
        public Task<bool> CanImportAsync(SchemaType type)
        {
            throw new NotImplementedException();
        }

        public Task GetCSEntryChangesAsync(SchemaType type)
        {
            throw new NotImplementedException();
        }

        public Task InitializeAsync(ImportContext context)
        {
            throw new NotImplementedException();
        }
    }
}
