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
        void Initialize(IImportContext context);

        bool CanImport(SchemaType type);

        Task GetCSEntryChangesAsync(SchemaType type);
    }
} 
