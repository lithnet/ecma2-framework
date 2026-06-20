using System.Collections.Generic;

namespace Lithnet.Ecma2Framework.Shim.Build.Tests
{
    /// <summary>
    /// The metadata facts read from a built shim assembly without loading or executing it: its simple
    /// assembly name, its public exported types (each with the interfaces it declares), and the simple
    /// names of the assemblies it references. These are exactly the facts Part C asserts against.
    /// </summary>
    internal sealed class ShimAssemblyMetadata
    {
        public ShimAssemblyMetadata(string assemblyName, IReadOnlyList<ShimExportedType> publicExportedTypes, IReadOnlyList<string> referencedAssemblyNames)
        {
            this.AssemblyName = assemblyName;
            this.PublicExportedTypes = publicExportedTypes;
            this.ReferencedAssemblyNames = referencedAssemblyNames;
        }

        /// <summary>
        /// The assembly's simple name (the value of the AssemblyDefinition Name field). For the shim this
        /// must equal the management agent name.
        /// </summary>
        public string AssemblyName { get; }

        /// <summary>
        /// Every public (externally visible) type defined in the assembly, with the interfaces each declares.
        /// </summary>
        public IReadOnlyList<ShimExportedType> PublicExportedTypes { get; }

        /// <summary>
        /// The simple names of every assembly referenced by this assembly. The closure proof asserts none
        /// of these begin with "Lithnet.".
        /// </summary>
        public IReadOnlyList<string> ReferencedAssemblyNames { get; }
    }
}
