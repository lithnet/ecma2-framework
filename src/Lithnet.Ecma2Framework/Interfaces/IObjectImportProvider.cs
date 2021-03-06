﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    public interface IObjectImportProvider
    {
        void Initialize(IImportContext context);

        bool CanImport(SchemaType type);

        void GetCSEntryChanges(SchemaType type);
    }
} 
