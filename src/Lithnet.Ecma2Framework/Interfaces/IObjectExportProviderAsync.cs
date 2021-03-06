﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectExportProviderAsync
    {
        void Initialize(IExportContext context);

        bool CanExport(CSEntryChange csentry);

        Task<CSEntryChangeResult> PutCSEntryChangeAsync(CSEntryChange csentry);
    }
}