using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Enumerates every named type defined in an assembly symbol, descending through namespaces and nested
    /// types. Used by the generator to run its symbol-based discovery over a REFERENCED consumer assembly
    /// (Design C: the host project references the consumer library, and the consumer's startup/providers are
    /// discovered from that reference's metadata rather than from in-project source).
    /// </summary>
    internal static class ReferencedAssemblyTypeWalker
    {
        /// <summary>
        /// Returns all named types declared in <paramref name="assembly"/>, including nested types, in
        /// depth-first order. Namespaces are traversed recursively; the result excludes the global namespace
        /// itself and any non-type members.
        /// </summary>
        public static IEnumerable<INamedTypeSymbol> GetAllTypes(IAssemblySymbol assembly)
        {
            return GetTypesInNamespace(assembly.GlobalNamespace);
        }

        private static IEnumerable<INamedTypeSymbol> GetTypesInNamespace(INamespaceSymbol namespaceSymbol)
        {
            foreach (INamedTypeSymbol type in namespaceSymbol.GetTypeMembers())
            {
                foreach (INamedTypeSymbol nestedOrSelf in ExpandNested(type))
                {
                    yield return nestedOrSelf;
                }
            }

            foreach (INamespaceSymbol childNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (INamedTypeSymbol type in GetTypesInNamespace(childNamespace))
                {
                    yield return type;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> ExpandNested(INamedTypeSymbol type)
        {
            yield return type;

            foreach (INamedTypeSymbol nested in type.GetTypeMembers())
            {
                foreach (INamedTypeSymbol deeper in ExpandNested(nested))
                {
                    yield return deeper;
                }
            }
        }
    }
}
