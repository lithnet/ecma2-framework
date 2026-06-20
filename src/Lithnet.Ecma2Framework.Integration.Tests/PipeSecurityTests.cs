using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using Lithnet.Ecma2Framework.Shim;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Integration.Tests
{
    /// <summary>
    /// Verifies the same-identity gate's primary, OS-enforced control: the worker pipe's ACL grants access
    /// to ONLY the creating user's SID and to no other account. The connect-time rejection of a different
    /// account is enforced by the OS from this ACL and cannot be exercised in-process without a second user
    /// account; this test asserts the ACL the host actually applies, which is what the OS enforces.
    /// </summary>
    [TestClass]
    public class PipeSecurityTests
    {
        [TestMethod]
        public void PipeAcl_GrantsAccessToOnlyTheCurrentUser()
        {
            SecurityIdentifier currentUser;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                currentUser = identity.User;
            }

            PipeSecurity security = JsonRpcPipeClient.CreateCurrentUserOnlyPipeSecurity();

            AuthorizationRuleCollection rules = security.GetAccessRules(true, false, typeof(SecurityIdentifier));

            Assert.AreEqual(1, rules.Count, "The pipe ACL must contain exactly one access rule (the current user only).");

            PipeAccessRule rule = (PipeAccessRule)rules[0];

            Assert.AreEqual(currentUser, rule.IdentityReference, "The single access rule must be for the current user's SID.");
            Assert.AreEqual(AccessControlType.Allow, rule.AccessControlType, "The rule must be an Allow rule.");
            Assert.AreEqual(PipeAccessRights.FullControl, rule.PipeAccessRights, "The current user must have full control.");

            Assert.AreEqual(
                currentUser,
                security.GetOwner(typeof(SecurityIdentifier)),
                "The pipe owner must be the current user's SID.");
        }
    }
}
