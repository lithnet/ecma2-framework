﻿using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework
{
    public interface IOperationInitializer
    {
        Task InitializeImportAsync(ImportContext context);

        Task InitializeExportAsync(ExportContext context);

        Task InitializePasswordOperationAsync(PasswordContext context);

        Task InitializeSchemaOperationAsync(SchemaContext context);
    }
}
