using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lithnet.Ecma2Framework.Interfaces
{
    public interface IConnectionInitializer
    {
        Task InitializeImportAsync(IImportContext context);

        Task InitializeExportAsync(IExportContext context);

        Task InitializePasswordOperationAsync(IPasswordContext context);
    }
}
