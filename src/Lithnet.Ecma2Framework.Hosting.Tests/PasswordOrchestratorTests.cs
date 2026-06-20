using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.TestConsumer;
using Lithnet.Ecma2Framework.Hosting;
using Lithnet.Ecma2Framework.Hosting.PasswordIdentity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// Unit tests for <see cref="Ecma2PasswordOrchestrator"/> driven through the
    /// TestConsumer DI container.
    ///
    /// These tests verify that the orchestrator dispatches SetPassword and ChangePassword
    /// correctly to <c>TestPasswordProviderImpl</c>, that the SHA-256 hashes recorded by
    /// that provider match the known inputs, and that the "failme" DN causes a throw.
    ///
    /// Security: no plaintext password is ever placed in an assertion message.  The tests
    /// compute the expected SHA-256 hash locally and compare it against the hash stored in
    /// <see cref="IPasswordCallRecorder"/>.
    /// </summary>
    [TestClass]
    public sealed class PasswordOrchestratorTests
    {
        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private static WorkerHost BuildWorkerHost()
        {
            WorkerHost host = WorkerHost.Create(new TestConsumerStartup(), new DefaultConfigRegistrationProvider());
            host.BuildContainer(new ConfigParameterKeyedCollection());
            return host;
        }

        private static Ecma2PasswordOrchestrator BuildAndOpenOrchestrator(WorkerHost host)
        {
            Ecma2PasswordOrchestrator orchestrator = new Ecma2PasswordOrchestrator(host.Services);
            PasswordContext context = PasswordContext.Create(null);
            Task.Run(async () => await orchestrator.OpenAsync(context)).GetAwaiter().GetResult();
            return orchestrator;
        }

        private static CSEntry BuildEntry(string dn, string objectType = "user")
        {
            CSEntryIdentity identity = new CSEntryIdentity();
            identity.DN = dn;
            identity.RDN = dn;
            identity.ObjectType = objectType;
            identity.ObjectClass.Add(objectType);
            return DetachedCSEntryFactory.Create(identity);
        }

        private static SecureString MakeSecure(string s)
        {
            SecureString secure = new SecureString();

            if (s != null)
            {
                foreach (char c in s)
                {
                    secure.AppendChar(c);
                }
            }

            secure.MakeReadOnly();
            return secure;
        }

        private static string ComputeExpectedSha256(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(utf8Bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }

        private static IPasswordCallRecorder GetRecorder(WorkerHost host)
        {
            foreach (IObjectPasswordProvider provider in host.GetPasswordProviders())
            {
                IPasswordCallRecorder recorder = provider as IPasswordCallRecorder;

                if (recorder != null)
                {
                    return recorder;
                }
            }

            Assert.Fail("No IPasswordCallRecorder found in the registered password providers.");
            return null;
        }

        // -------------------------------------------------------------------------
        // OpenAsync: throws when no providers registered
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task OpenAsync_NoProviders_ThrowsInvalidOperationException()
        {
            // Build a DI container with no password providers.
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            ServiceProvider sp = services.BuildServiceProvider();

            Ecma2PasswordOrchestrator orchestrator = new Ecma2PasswordOrchestrator(sp);
            PasswordContext context = PasswordContext.Create(null);

            bool threw = false;

            try
            {
                await orchestrator.OpenAsync(context);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "OpenAsync must throw InvalidOperationException when no providers are registered.");
        }

        // -------------------------------------------------------------------------
        // SetPasswordAsync: dispatches to TestPasswordProviderImpl
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task SetPasswordAsync_RecordsDn()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=alice");
            string passwordValue = "test-set-password";

            using (SecureString pw = MakeSecure(passwordValue))
            {
                await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.None);
            }

            Assert.AreEqual("CN=alice", recorder.LastDn, "SetPasswordAsync must record the DN.");
        }

        [TestMethod]
        public async Task SetPasswordAsync_RecordsObjectType()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=alice", "person");
            string passwordValue = "test-set-password";

            using (SecureString pw = MakeSecure(passwordValue))
            {
                await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.None);
            }

            Assert.AreEqual("person", recorder.LastObjectType, "SetPasswordAsync must record the ObjectType.");
        }

        [TestMethod]
        public async Task SetPasswordAsync_RecordsOptions()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=bob");
            string passwordValue = "another-password";

            using (SecureString pw = MakeSecure(passwordValue))
            {
                await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.ForceChangeAtLogOn);
            }

            Assert.AreEqual(
                PasswordOptions.ForceChangeAtLogOn.ToString(),
                recorder.LastOptions,
                "SetPasswordAsync must record the PasswordOptions.");
        }

        [TestMethod]
        public async Task SetPasswordAsync_NewPasswordHashMatchesExpected()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=alice");
            string passwordValue = "test-set-password-hash-check";

            using (SecureString pw = MakeSecure(passwordValue))
            {
                await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.None);
            }

            string expectedHash = ComputeExpectedSha256(passwordValue);

            // Assert hash equality — if they match, the secret arrived at the provider intact.
            // SECURITY: the plaintext is NOT included in any assertion message.
            Assert.AreEqual(
                expectedHash,
                recorder.LastNewPasswordSha256,
                "The SHA-256 of the new password at the provider must match the locally computed expected hash.");
        }

        [TestMethod]
        public async Task SetPasswordAsync_OldPasswordHashIsEmpty()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=alice");

            using (SecureString pw = MakeSecure("any-password"))
            {
                await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.None);
            }

            Assert.AreEqual(
                string.Empty,
                recorder.LastOldPasswordSha256,
                "SetPasswordAsync must leave the OldPasswordSha256 empty.");
        }

        // -------------------------------------------------------------------------
        // SetPasswordAsync: failme DN throws
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task SetPasswordAsync_FailmeDn_ThrowsInvalidOperationException()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);

            CSEntry entry = BuildEntry("CN=failme-user");
            bool threw = false;

            try
            {
                using (SecureString pw = MakeSecure("irrelevant"))
                {
                    await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.None);
                }
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "SetPasswordAsync with a 'failme' DN must throw InvalidOperationException.");
        }

        // -------------------------------------------------------------------------
        // ChangePasswordAsync: dispatches to TestPasswordProviderImpl
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task ChangePasswordAsync_RecordsDn()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=charlie");

            using (SecureString oldPw = MakeSecure("old-secret"))
            using (SecureString newPw = MakeSecure("new-secret"))
            {
                await orchestrator.ChangePasswordAsync(entry, oldPw, newPw);
            }

            Assert.AreEqual("CN=charlie", recorder.LastDn, "ChangePasswordAsync must record the DN.");
        }

        [TestMethod]
        public async Task ChangePasswordAsync_OldPasswordHashMatchesExpected()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=diana");
            string oldValue = "old-secret-value";
            string newValue = "new-secret-value";

            using (SecureString oldPw = MakeSecure(oldValue))
            using (SecureString newPw = MakeSecure(newValue))
            {
                await orchestrator.ChangePasswordAsync(entry, oldPw, newPw);
            }

            string expectedOldHash = ComputeExpectedSha256(oldValue);

            // SECURITY: assertion is over hashes, not plaintext.
            Assert.AreEqual(
                expectedOldHash,
                recorder.LastOldPasswordSha256,
                "The SHA-256 of the old password at the provider must match the locally computed expected hash.");
        }

        [TestMethod]
        public async Task ChangePasswordAsync_NewPasswordHashMatchesExpected()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);
            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntry entry = BuildEntry("CN=diana");
            string oldValue = "old-secret-value";
            string newValue = "new-secret-value";

            using (SecureString oldPw = MakeSecure(oldValue))
            using (SecureString newPw = MakeSecure(newValue))
            {
                await orchestrator.ChangePasswordAsync(entry, oldPw, newPw);
            }

            string expectedNewHash = ComputeExpectedSha256(newValue);

            // SECURITY: assertion is over hashes, not plaintext.
            Assert.AreEqual(
                expectedNewHash,
                recorder.LastNewPasswordSha256,
                "The SHA-256 of the new password at the provider must match the locally computed expected hash.");
        }

        [TestMethod]
        public async Task ChangePasswordAsync_FailmeDn_ThrowsInvalidOperationException()
        {
            WorkerHost host = BuildWorkerHost();
            Ecma2PasswordOrchestrator orchestrator = BuildAndOpenOrchestrator(host);

            CSEntry entry = BuildEntry("CN=failme-change-user");
            bool threw = false;

            try
            {
                using (SecureString oldPw = MakeSecure("old"))
                using (SecureString newPw = MakeSecure("new"))
                {
                    await orchestrator.ChangePasswordAsync(entry, oldPw, newPw);
                }
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "ChangePasswordAsync with a 'failme' DN must throw InvalidOperationException.");
        }

        // -------------------------------------------------------------------------
        // Called before OpenAsync: throws
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task SetPasswordAsync_BeforeOpen_ThrowsInvalidOperationException()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IObjectPasswordProvider, Lithnet.Ecma2Framework.TestConsumer.TestPasswordProviderImpl>();
            ServiceProvider sp = services.BuildServiceProvider();

            Ecma2PasswordOrchestrator orchestrator = new Ecma2PasswordOrchestrator(sp);

            CSEntry entry = BuildEntry("CN=alice");
            bool threw = false;

            try
            {
                using (SecureString pw = MakeSecure("any"))
                {
                    await orchestrator.SetPasswordAsync(entry, pw, PasswordOptions.None);
                }
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "SetPasswordAsync must throw InvalidOperationException when called before OpenAsync.");
        }

        [TestMethod]
        public async Task ChangePasswordAsync_BeforeOpen_ThrowsInvalidOperationException()
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IObjectPasswordProvider, Lithnet.Ecma2Framework.TestConsumer.TestPasswordProviderImpl>();
            ServiceProvider sp = services.BuildServiceProvider();

            Ecma2PasswordOrchestrator orchestrator = new Ecma2PasswordOrchestrator(sp);

            CSEntry entry = BuildEntry("CN=alice");
            bool threw = false;

            try
            {
                using (SecureString oldPw = MakeSecure("old"))
                using (SecureString newPw = MakeSecure("new"))
                {
                    await orchestrator.ChangePasswordAsync(entry, oldPw, newPw);
                }
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.IsTrue(threw, "ChangePasswordAsync must throw InvalidOperationException when called before OpenAsync.");
        }
    }
}
