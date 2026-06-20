using System.Collections.ObjectModel;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// The public host-facing import implementation. FIM instantiates this type to service import calls.
    /// Each interface member forwards to an internal <see cref="ImportConnection"/>, which marshals the call
    /// to the out-of-process worker over the named pipe.
    /// </summary>
    /// <remarks>
    /// Plain shared source compiled into every per-MA shim (not generated); the fixed type name is safe
    /// because FIM resolves the extension type from the specific per-MA shim assembly. See
    /// <see cref="Ecma2Implementation"/> for the full rationale.
    /// </remarks>
    public sealed class Ecma2ImportImplementation : IMAExtensible2CallImport
    {
        private readonly ImportConnection connection;

        public Ecma2ImportImplementation()
        {
            string workerPath = WorkerPathResolver.Resolve();
            this.connection = new ImportConnection(workerPath);
        }

        int IMAExtensible2CallImport.ImportDefaultPageSize => this.connection.ImportDefaultPageSize;

        int IMAExtensible2CallImport.ImportMaxPageSize => this.connection.ImportMaxPageSize;

        OpenImportConnectionResults IMAExtensible2CallImport.OpenImportConnection(KeyedCollection<string, ConfigParameter> configParameters, Schema types, OpenImportConnectionRunStep importRunStep)
        {
            return this.connection.OpenImportConnection(configParameters, types, importRunStep);
        }

        GetImportEntriesResults IMAExtensible2CallImport.GetImportEntries(GetImportEntriesRunStep importRunStep)
        {
            return this.connection.GetImportEntries(importRunStep);
        }

        CloseImportConnectionResults IMAExtensible2CallImport.CloseImportConnection(CloseImportConnectionRunStep importRunStep)
        {
            return this.connection.CloseImportConnection(importRunStep);
        }
    }
}
