using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.TestConsumer;
using Lithnet.Ecma2Framework.Serialization;
using Lithnet.Ecma2Framework.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lithnet.Ecma2Framework.Hosting.Tests
{
    /// <summary>
    /// Unit tests for the worker's <see cref="SchemaRpcTarget"/> password handlers, driven in-process
    /// (no pipe). These prove the seam that <see cref="PasswordOrchestratorTests"/> does NOT cover:
    /// the RPC handler accepts the secret as a plain wire <c>string</c> argument, rebuilds a
    /// <see cref="SecureString"/> from it, and hands the EXACT byte-for-byte secret to the provider.
    ///
    /// The handlers are exercised through the same string-param surface StreamJsonRpc dispatches to
    /// (<see cref="SchemaRpcTarget.SetPassword"/> / <see cref="SchemaRpcTarget.ChangePassword"/>), with
    /// the identity serialised exactly as the shim serialises it (a
    /// <see cref="MmsPipeSerializer"/> XML string of a <see cref="CSEntryIdentity"/>).
    ///
    /// Security: no plaintext password is ever placed in an assertion message. The tests compute the
    /// expected SHA-256 hash locally and compare it against the hash the provider recorded.
    /// </summary>
    [TestClass]
    public sealed class PasswordRpcTargetTests
    {
        // -------------------------------------------------------------------------
        // Helpers — copied verbatim in style from PasswordOrchestratorTests.
        // -------------------------------------------------------------------------

        private static WorkerHost BuildWorkerHost()
        {
            WorkerHost host = WorkerHost.Create(new TestConsumerStartup(), new DefaultConfigRegistrationProvider());
            host.BuildContainer(new ConfigParameterKeyedCollection());
            return host;
        }

        private static CSEntryIdentity BuildEntry(string dn, string objectType = "user")
        {
            CSEntryIdentity entry = new CSEntryIdentity();
            entry.DN = dn;
            entry.RDN = dn;
            entry.ObjectType = objectType;
            entry.ObjectClass.Add(objectType);
            return entry;
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

        /// <summary>
        /// Serialises an empty config-parameter list the same way the shim's
        /// <c>PasswordConnection.SerializeConfigParameters</c> does.
        /// </summary>
        private static string EmptyConfigParametersXml()
        {
            return MmsPipeSerializer.SerializeXml<List<ConfigParameter>>(new List<ConfigParameter>());
        }

        private static string ExtensionsDirectory()
        {
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        // -------------------------------------------------------------------------
        // SetPassword: the wire string secret is rebuilt and reaches the provider intact.
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task SetPassword_WireStringSecret_ReachesProviderIntact()
        {
            WorkerHost host = BuildWorkerHost();

            // ctor: internal SchemaRpcTarget(WorkerHost workerHost)
            SchemaRpcTarget target = new SchemaRpcTarget(host);

            // OpenPassword rebuilds the DI container, so the recorder MUST be resolved after this call.
            await target.OpenPassword(EmptyConfigParametersXml(), null, ExtensionsDirectory());

            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntryIdentity entry = BuildEntry("CN=alice");
            string identityXml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(entry);

            string passwordValue = "known-plaintext-value";

            await target.SetPassword(identityXml, passwordValue, PasswordOptions.None.ToString());

            string expectedHash = ComputeExpectedSha256(passwordValue);

            // If the hashes match, the RPC handler rebuilt the exact secret from the wire string and
            // delivered it to the provider intact.
            // SECURITY: the plaintext is NOT included in any assertion message.
            Assert.AreEqual(
                expectedHash,
                recorder.LastNewPasswordSha256,
                "The SHA-256 of the new password at the provider must match the locally computed expected hash.");

            Assert.AreEqual(
                "CN=alice",
                recorder.LastDn,
                "SetPassword must deliver the deserialised identity DN to the provider.");

            await target.ClosePassword();
        }

        // -------------------------------------------------------------------------
        // ChangePassword: BOTH wire string secrets are rebuilt and reach the provider intact.
        // -------------------------------------------------------------------------

        [TestMethod]
        public async Task ChangePassword_WireStringSecrets_ReachProviderIntact()
        {
            WorkerHost host = BuildWorkerHost();

            SchemaRpcTarget target = new SchemaRpcTarget(host);

            await target.OpenPassword(EmptyConfigParametersXml(), null, ExtensionsDirectory());

            IPasswordCallRecorder recorder = GetRecorder(host);

            CSEntryIdentity entry = BuildEntry("CN=diana");
            string identityXml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(entry);

            string oldValue = "known-old-value";
            string newValue = "known-new-value";

            await target.ChangePassword(identityXml, oldValue, newValue);

            string expectedOldHash = ComputeExpectedSha256(oldValue);
            string expectedNewHash = ComputeExpectedSha256(newValue);

            // SECURITY: assertions are over hashes, not plaintext.
            Assert.AreEqual(
                expectedOldHash,
                recorder.LastOldPasswordSha256,
                "The SHA-256 of the old password at the provider must match the locally computed expected hash.");

            Assert.AreEqual(
                expectedNewHash,
                recorder.LastNewPasswordSha256,
                "The SHA-256 of the new password at the provider must match the locally computed expected hash.");

            await target.ClosePassword();
        }
    }
}
