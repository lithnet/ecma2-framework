using System.Collections.Generic;

namespace Lithnet.Ecma2Framework.Shim.Build.Tests
{
    /// <summary>
    /// A single public exported type, identified by its full (namespace-qualified) name, together with the
    /// full names of the interfaces it directly declares. Built from pure metadata reads - no type loading.
    /// </summary>
    internal sealed class ShimExportedType
    {
        public ShimExportedType(string fullName, IReadOnlyList<string> implementedInterfaceNames)
        {
            this.FullName = fullName;
            this.ImplementedInterfaceNames = implementedInterfaceNames;
        }

        /// <summary>
        /// The type's full name, for example <c>Test.Shim.MA.Ecma2Implementation</c>.
        /// </summary>
        public string FullName { get; }

        /// <summary>
        /// The full names of the interfaces the type directly declares (from its InterfaceImplementation
        /// rows). Interface names referenced from another assembly (the host MMS) are resolved to their
        /// namespace-qualified name via the TypeReference table.
        /// </summary>
        public IReadOnlyList<string> ImplementedInterfaceNames { get; }
    }
}
