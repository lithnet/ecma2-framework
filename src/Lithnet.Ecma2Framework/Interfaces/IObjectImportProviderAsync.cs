using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectImportProviderAsync
    {
        bool CanImport(SchemaType type);

        Task GetCSEntryChangesAsync(ImportContext context, SchemaType type);
    }
} 
