using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    /// <summary>
    /// The exception-hierarchy fidelity backbone (mapping Section D). Reflects BOTH host exception trees
    /// (<c>MetadirectoryServicesException</c> + <c>ExtensionException</c> subclasses) and asserts the
    /// reconstruction registry/factory has a path for EVERY one (expected 42) and carries EVERY declared backing
    /// field. A host-revision-added exception type, or a newly declared field on an existing type, fails the
    /// build until it is mapped.
    /// </summary>
    internal static class ExceptionCompletenessGuard
    {
        private const BindingFlags DeclaredInstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        /// <summary>
        /// Returns every host exception type (chain hits either tree root). This is the authoritative set the
        /// registry must cover.
        /// </summary>
        public static List<Type> EnumerateHostExceptionTypes()
        {
            Assembly hostAssembly = typeof(MetadirectoryServicesException).Assembly;

            return hostAssembly.GetTypes()
                .Where(t => t.IsClass)
                .Where(IsInEitherTree)
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Returns the host exception types the registry does NOT resolve (a coverage gap). Empty means the
        /// registry covers every reflected host exception type.
        /// </summary>
        public static List<string> UnmappedTypes()
        {
            List<string> unmapped = new List<string>();

            foreach (Type type in EnumerateHostExceptionTypes())
            {
                if (MmsExceptionTypeRegistry.Resolve(type.Name) == null)
                {
                    unmapped.Add(type.Name);
                }
            }

            return unmapped;
        }

        /// <summary>
        /// Returns, per host type, the declared backing fields the registry does NOT carry. Empty means every
        /// declared field on every host exception type is carried.
        /// </summary>
        public static SortedDictionary<string, List<string>> UncarriedDeclaredFields()
        {
            SortedDictionary<string, List<string>> result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (Type type in EnumerateHostExceptionTypes())
            {
                HashSet<string> mapped = new HashSet<string>(MmsExceptionTypeRegistry.DeclaredFields(type.Name), StringComparer.Ordinal);

                List<string> missing = new List<string>();

                foreach (FieldInfo field in type.GetFields(DeclaredInstanceFields))
                {
                    // Const/static excluded by DeclaredInstanceFields. Every declared instance backing field
                    // must be carried by the envelope (mapping Section D: carry every declared field).
                    if (!mapped.Contains(field.Name))
                    {
                        missing.Add(field.Name);
                    }
                }

                if (missing.Count > 0)
                {
                    result[type.Name] = missing;
                }
            }

            return result;
        }

        private static bool IsInEitherTree(Type type)
        {
            return typeof(MetadirectoryServicesException).IsAssignableFrom(type)
                   || typeof(ExtensionException).IsAssignableFrom(type);
        }
    }
}
