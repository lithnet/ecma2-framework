using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The public host-facing export implementation. FIM instantiates this type to service export calls.
    /// Each interface member forwards to an internal <see cref="ExportConnection"/>, which marshals the call
    /// to the out-of-process worker over the named pipe.
    /// </summary>
    /// <remarks>
    /// Plain shared source compiled into every per-MA shim (not generated); the fixed type name is safe
    /// because FIM resolves the extension type from the specific per-MA shim assembly. See
    /// <see cref="Ecma2Implementation"/> for the full rationale.
    /// </remarks>
    public sealed class Ecma2ExportImplementation : IMAExtensible2CallExport
    {
        private readonly ExportConnection connection;

        public Ecma2ExportImplementation()
        {
            string workerPath = WorkerPathResolver.Resolve();
            this.connection = new ExportConnection(workerPath);
        }

        int IMAExtensible2CallExport.ExportDefaultPageSize => this.connection.ExportDefaultPageSize;

        int IMAExtensible2CallExport.ExportMaxPageSize => this.connection.ExportMaxPageSize;

        void IMAExtensible2CallExport.OpenExportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenExportConnectionRunStep exportRunStep)
        {
            this.connection.OpenExportConnection(configParameters, types, exportRunStep);
        }

        PutExportEntriesResults IMAExtensible2CallExport.PutExportEntries(IList<CSEntryChange> csentries)
        {
            return this.connection.PutExportEntries(csentries);
        }

        void IMAExtensible2CallExport.CloseExportConnection(CloseExportConnectionRunStep exportRunStep)
        {
            this.connection.CloseExportConnection(exportRunStep);
        }
    }
}
