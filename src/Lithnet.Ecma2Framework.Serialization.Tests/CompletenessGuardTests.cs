using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Serialization.Tests
{
    /// <summary>
    /// The fidelity backbone, run as a hard build-failing gate: every covered host crossing-type's public data
    /// member is either carried by a <c>[DataMember]</c> on its <c>*Serializable</c> DTO or is a known
    /// derived-from-carried member. A gap fails the build with a per-type list of uncarried members.
    /// </summary>
    [TestClass]
    public class CompletenessGuardTests
    {
        private static Assembly HostAssembly
        {
            get { return typeof(global::Microsoft.MetadirectoryServices.CSEntryChange).Assembly; }
        }

        private static Assembly DtoAssembly
        {
            get { return typeof(MmsPipeSerializer).Assembly; }
        }

        [TestMethod]
        public void EveryCoveredHostTypeMemberIsCarriedOrDerived()
        {
            SortedDictionary<string, List<string>> uncarried =
                CompletenessGuard.CollectUncarriedMembers(HostAssembly, DtoAssembly);

            if (uncarried.Count == 0)
            {
                return;
            }

            StringBuilder message = new StringBuilder();
            message.AppendLine("Completeness guard found host data members not carried by a [DataMember] on the DTO");
            message.AppendLine("(and not registered as derived-from-carried). Either carry them or, if genuinely");
            message.AppendLine("derived from another carried member, add them to CompletenessGuard.DerivedFromCarried:");
            foreach (KeyValuePair<string, List<string>> entry in uncarried)
            {
                message.AppendLine($"  {entry.Key}: {string.Join(", ", entry.Value)}");
            }

            Assert.Fail(message.ToString());
        }

        [TestMethod]
        public void ConfigParameterSecureValueIsCarried()
        {
            // Explicit, per the plan: SecureValue must be carried (not excluded). It is carried under the name
            // SecureValueContent; the guard's alias map must recognise it, so it must NOT appear as uncarried.
            SortedDictionary<string, List<string>> uncarried =
                CompletenessGuard.CollectUncarriedMembers(HostAssembly, DtoAssembly);

            bool secureValueUncarried = uncarried.TryGetValue("ConfigParameter", out List<string> members)
                                        && members.Contains("SecureValue");

            Assert.IsFalse(secureValueUncarried, "ConfigParameter.SecureValue must be carried, not excluded.");
        }

        [TestMethod]
        public void HarnessResolvesAllCoveredHostTypes()
        {
            int count = CompletenessGuard.EnumerateCoveredHostTypes(HostAssembly).Count();

            // 8 ported ACMA DTOs + 11 Phase-2 additions (MACapabilities, ConfigParameter,
            // ConfigParameterDefinition, ParameterValidationResult, the 5 run-steps, Partition, HierarchyNode)
            // + ChangeTypeDescription (nested Schema member, now carried with a detached impl).
            Assert.AreEqual(20, count, "The covered host-type set did not resolve to the expected count.");
        }
    }
}
