using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectExportProvider
    {
        bool CanExport(CSEntryChange csentry);

        CSEntryChangeResult PutCSEntryChange(CSEntryChange csentry, ExportContext context);
    }
} 
