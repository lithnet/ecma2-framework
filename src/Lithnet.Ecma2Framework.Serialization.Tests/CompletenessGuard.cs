using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    /// <summary>
    /// The fidelity backbone. For every host crossing-type that has a <c>*Serializable</c> DTO, this reflects
    /// the REAL host type's public data members and asserts the DTO carries a matching <c>[DataMember]</c>
    /// (property), OR the member is a known derived-from-carried member. Salvaged from the orphaned
    /// <c>WireCompletenessGuard</c> (which inspected <c>[DataMember]</c> fields on Transport types); this
    /// version inspects <c>[DataMember]</c> properties on the <c>*Serializable</c> DTOs and resolves a host
    /// type to its DTO by the <c>&lt;HostType&gt;Serializable</c> naming convention.
    /// </summary>
    internal static class CompletenessGuard
    {
        /// <summary>
        /// The host crossing-types covered by this phase: the 8 ported ACMA DTOs + the 12 Phase-2 additions.
        /// Each must have a <c>&lt;Name&gt;Serializable</c> DTO. The exception hierarchy is a separate
        /// error-path piece and is NOT covered here.
        /// </summary>
        private static readonly string[] CoveredHostTypeNames =
        {
            // 8 ported ACMA DTOs
            "CSEntryChange",
            "AttributeChange",
            "ValueChange",
            "AnchorAttribute",
            "Schema",
            "SchemaType",
            "SchemaAttribute",
            "CSEntryChangeResult",
            // 12 Phase-2 additions
            "MACapabilities",
            "ConfigParameter",
            "ConfigParameterDefinition",
            "ParameterValidationResult",
            "OpenImportConnectionRunStep",
            "OpenExportConnectionRunStep",
            "CloseImportConnectionRunStep",
            "CloseExportConnectionRunStep",
            "GetImportEntriesRunStep",
            "Partition",
            "HierarchyNode",
            // Nested carried member of Schema; now has a DTO + detached impl.
            "ChangeTypeDescription",
        };

        /// <summary>
        /// The derived-from-carried registry: host members reconstructed host-side from other carried members,
        /// so they need no DTO member of their own. THE ONLY allowed exclusions. Keyed by host type simple
        /// name. A small registry per the plan: RDN derives from DN, SchemaType.AnchorAttributes derives from
        /// Attributes, ConfigParameterDefinition.CheckBoxDefault derives from DefaultValue.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> DerivedFromCarried =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                // RDN reconstructed from the carried DN; ChangedAttributeNames recomputed from AttributeChanges
                // when the explicit list is absent (the existing CSEntryChangeSerializable does not carry the
                // explicit list separately).
                ["CSEntryChange"] = new HashSet<string>(StringComparer.Ordinal) { "RDN", "ChangedAttributeNames" },
                // AnchorAttributes is a computed view filtering Attributes where IsAnchor (the only genuine
                // derived-from-carried member here; PossibleDNComponentsForProvisioning is a real list and is
                // carried).
                ["SchemaType"] = new HashSet<string>(StringComparer.Ordinal) { "AnchorAttributes" },
                // CheckBoxDefault is computed as ("1" == DefaultValue).
                ["ConfigParameterDefinition"] = new HashSet<string>(StringComparer.Ordinal) { "CheckBoxDefault" },
            };

        /// <summary>
        /// Host member name -> the DTO [DataMember] property name that carries it, where they differ. Used so a
        /// non-identically-named carried member still counts. <c>ConfigParameter.SecureValue</c> MUST be
        /// carried; the DTO carries it as <c>SecureValueContent</c>.
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, string>> CarriedUnderDifferentName =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal)
            {
                ["ConfigParameter"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["SecureValue"] = "SecureValueContent",
                },
                // The ported ACMA DTO carries the updated-anchor attribute changes as AnchorChanges.
                ["CSEntryChangeResult"] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["AnchorAttributes"] = "AnchorChanges",
                },
            };

        private const string DtoNamespace = "Lithnet.Ecma2Framework.Serialization";

        /// <summary>
        /// Runs the guard, returning per host type the list of public data members not carried by a DTO
        /// <c>[DataMember]</c> (and not derived-from-carried). An empty result means full fidelity.
        /// </summary>
        public static SortedDictionary<string, List<string>> CollectUncarriedMembers(Assembly hostAssembly, Assembly dtoAssembly)
        {
            SortedDictionary<string, List<string>> result = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);

            foreach (Type hostType in EnumerateCoveredHostTypes(hostAssembly))
            {
                List<string> uncarried = UncarriedMembersFor(hostType, dtoAssembly);
                if (uncarried.Count > 0)
                {
                    result[hostType.Name] = uncarried;
                }
            }

            return result;
        }

        public static IEnumerable<Type> EnumerateCoveredHostTypes(Assembly hostAssembly)
        {
            Type[] all = hostAssembly.GetTypes();

            foreach (string name in CoveredHostTypeNames)
            {
                Type type = all.FirstOrDefault(t => t.Name == name && t.Namespace == "Microsoft.MetadirectoryServices");
                if (type == null)
                {
                    throw new InvalidOperationException($"Host crossing-type '{name}' not found in the host assembly.");
                }

                yield return type;
            }
        }

        private static List<string> UncarriedMembersFor(Type hostType, Assembly dtoAssembly)
        {
            HashSet<string> derived = DerivedFromCarried.TryGetValue(hostType.Name, out HashSet<string> d)
                ? d
                : new HashSet<string>(StringComparer.Ordinal);

            CarriedUnderDifferentName.TryGetValue(hostType.Name, out Dictionary<string, string> aliases);

            HashSet<string> carried = CarriedDataMemberNames(hostType.Name, dtoAssembly);

            List<string> uncarried = new List<string>();

            foreach (string member in HostDataMembers(hostType))
            {
                if (derived.Contains(member))
                {
                    continue;
                }

                if (carried.Contains(member))
                {
                    continue;
                }

                if (aliases != null && aliases.TryGetValue(member, out string alias) && carried.Contains(alias))
                {
                    continue;
                }

                uncarried.Add(member);
            }

            return uncarried;
        }

        private static IEnumerable<string> HostDataMembers(Type hostType)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;

            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);

            foreach (PropertyInfo property in hostType.GetProperties(flags))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                names.Add(property.Name);
            }

            return names.OrderBy(n => n, StringComparer.Ordinal);
        }

        private static HashSet<string> CarriedDataMemberNames(string hostTypeName, Assembly dtoAssembly)
        {
            HashSet<string> carried = new HashSet<string>(StringComparer.Ordinal);

            string dtoName = hostTypeName + "Serializable";
            Type dtoType = dtoAssembly.GetTypes()
                .FirstOrDefault(t => t.Name == dtoName && t.Namespace == DtoNamespace);

            if (dtoType == null)
            {
                return carried;
            }

            foreach (PropertyInfo property in dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetCustomAttribute<DataMemberAttribute>() == null)
                {
                    continue;
                }

                carried.Add(property.Name);
            }

            return carried;
        }
    }
}
