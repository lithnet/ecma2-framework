using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security;
using Lithnet.Ecma2Framework;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Implements the host <see cref="IMAExtensible2Password"/> contract by delegating each password
    /// lifecycle call to the out-of-process worker via the named-pipe JSON-RPC transport.
    /// </summary>
    /// <remarks>
    /// Transport (Path C): the host hands these methods a live <see cref="CSEntry"/> — an abstract
    /// engine object with no constructible form, so it cannot cross the pipe. The shim extracts the
    /// identity subset a password provider needs (DN, RDN, ObjectType, ObjectClass, MA name, and every
    /// present attribute WITH its value(s) — see <see cref="RealCSEntryToIdentity"/>) into a framework-owned
    /// <see cref="CSEntryIdentity"/>, serialises it via <see cref="MmsPipeSerializer"/>, and sends THAT to
    /// the worker. The worker hands the reconstructed identity to the provider; nothing on the password
    /// path travels back to the host engine.
    ///
    /// Worker executable location: resolved from the constructor-injected path (tests) or the
    /// <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable. If neither is set,
    /// <see cref="OpenPasswordConnection"/> throws <see cref="InvalidOperationException"/> rather than
    /// silently using a wrong path.
    ///
    /// Security contract:
    /// <list type="bullet">
    ///   <item>Passwords are NEVER logged. No code path here (or the classes it calls) emits a password to
    ///     any log, trace, debug output, or exception message.</item>
    ///   <item><see cref="SecureString"/> values are converted to plaintext only at the wire boundary in
    ///     <see cref="SetPasswordCore"/> / <see cref="ChangePasswordCore"/>; the unmanaged buffer is zeroed
    ///     by <see cref="SecureStringConverter.ToPlainText"/> in a <c>finally</c> block, and the managed
    ///     string copy is scoped to a local and not retained beyond the RPC call.</item>
    ///   <item>Exception messages re-thrown to the host carry only the worker-supplied error text, which is
    ///     secret-free by construction.</item>
    /// </list>
    ///
    /// SECURITY: passwords cross the local pipe as plaintext at the boundary, so the channel is restricted to
    /// a single identity — the pipe ACL admits only the creating user's SID and the server verifies the
    /// connected worker's SID, failing closed otherwise. See the same-identity gate documented on
    /// <see cref="JsonRpcPipeClient"/>.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenPasswordConnection"/> throws on any failure; if the worker was started before
    ///     the failure, its <see cref="WorkerProcessHost"/> is disposed, killing the process via the Job
    ///     Object.</item>
    ///   <item><see cref="SetPassword"/> / <see cref="ChangePassword"/> catch worker errors (surfaced by
    ///     <see cref="JsonRpcPipeClient"/> as <see cref="InvalidOperationException"/>) and re-throw as
    ///     <see cref="ExtensibleExtensionException"/> with the worker-supplied (secret-free) message.</item>
    ///   <item><see cref="ClosePasswordConnection"/> disposes the worker host regardless of whether the pipe
    ///     call succeeds, so the worker process is always killed.</item>
    /// </list>
    ///
    /// Thread safety: this class is used by the MIM engine on a single thread per password operation; no
    /// locking is applied.
    /// </remarks>
    internal sealed class PasswordConnection
    {
        private readonly WorkerSession session;

        // -------------------------------------------------------------------------
        // Constructors
        // -------------------------------------------------------------------------

        /// <summary>
        /// Initialises a <see cref="PasswordConnection"/> that resolves the worker executable path
        /// from the <c>LITHNET_ECMA2_WORKER_EXE</c> environment variable.
        /// </summary>
        public PasswordConnection()
        {
            this.session = new WorkerSession();
        }

        /// <summary>
        /// Initialises a <see cref="PasswordConnection"/> with an explicit worker executable path.
        /// Intended for tests.
        /// </summary>
        /// <param name="workerExePath">Absolute path to the net8 worker executable.</param>
        internal PasswordConnection(string workerExePath)
        {
            this.session = new WorkerSession(workerExePath);
        }

        // -------------------------------------------------------------------------
        // IMAExtensible2Password
        // -------------------------------------------------------------------------

        /// <summary>
        /// Spawns the worker process, establishes the pipe connection, and sends the
        /// <c>OpenPassword</c> JSON-RPC call.
        /// </summary>
        /// <param name="configParameters">The MA configuration parameters supplied by the host engine.</param>
        /// <param name="partition">
        /// The partition in which the password operation takes place. Serialised and sent to the worker;
        /// a null partition is sent as null.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker executable path cannot be resolved from the environment, when the worker
        /// fails to start, or when the JSON-RPC call returns an error.
        /// </exception>
        public void OpenPasswordConnection(
            KeyedCollection<string, ConfigParameter> configParameters,
            Partition partition)
        {
            if (configParameters == null)
            {
                throw new ArgumentNullException("configParameters");
            }

            JsonRpcPipeClient client = this.session.Open();

            string configParametersXml = ConfigParameterSerialization.Serialize(configParameters);
            string partitionXml = partition == null
                ? null
                : MmsPipeSerializer.SerializeXml<Partition>(partition);

            // Capture the engine's ExtensionsDirectory on the net48 side and inject it into the worker,
            // whose own Utils static would resolve to the WRONG directory.
            client.OpenPassword(configParametersXml, partitionXml, Utils.ExtensionsDirectory);
        }

        /// <summary>
        /// Sends <c>ClosePassword</c> to the worker, then unconditionally disposes the worker host
        /// (killing the worker process).
        /// </summary>
        public void ClosePasswordConnection()
        {
            try
            {
                if (this.session.Client != null)
                {
                    this.session.Client.ClosePassword();
                }
            }
            finally
            {
                // Dispose the worker host and pipe client unconditionally so the worker
                // process is always killed even if the ClosePassword call fails.
                this.session.Dispose();
            }
        }

        /// <summary>
        /// Calls <c>GetSecurityLevel</c> on the worker and returns the parsed
        /// <see cref="ConnectionSecurityLevel"/> enum value.
        /// </summary>
        /// <returns>The security level reported by the worker.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the worker returns a JSON-RPC error or the response is malformed.
        /// </exception>
        public ConnectionSecurityLevel GetConnectionSecurityLevel()
        {
            string level = this.session.Client.GetSecurityLevel();

            if (!Enum.TryParse(level, out ConnectionSecurityLevel result))
            {
                throw new InvalidOperationException(
                    string.Format("The worker returned an unrecognised connection security level '{0}'.", level));
            }

            return result;
        }

        /// <summary>
        /// Sets the password for the connector space entry represented by <paramref name="csentry"/>.
        /// </summary>
        /// <remarks>
        /// SECURITY: <paramref name="newPassword"/> is converted to plaintext inside
        /// <see cref="SetPasswordCore"/>, which zeros the unmanaged buffer in a <c>finally</c> block.
        /// No password value is retained beyond the RPC call.
        /// </remarks>
        /// <param name="csentry">The live connector space entry whose password is being set.</param>
        /// <param name="newPassword">The new password as a <see cref="SecureString"/>.</param>
        /// <param name="options">Password-change option flags.</param>
        public void SetPassword(CSEntry csentry, SecureString newPassword, PasswordOptions options)
        {
            CSEntryIdentity identity = RealCSEntryToIdentity.Read(csentry);
            SetPasswordCore(identity, newPassword, options);
        }

        /// <summary>
        /// Changes the password for the connector space entry represented by <paramref name="csentry"/>.
        /// </summary>
        /// <remarks>
        /// SECURITY: both <paramref name="oldPassword"/> and <paramref name="newPassword"/> are converted to
        /// plaintext inside <see cref="ChangePasswordCore"/>, each zeroing its unmanaged buffer in a
        /// <c>finally</c> block. No password value is retained beyond the RPC call.
        /// </remarks>
        /// <param name="csentry">The live connector space entry whose password is being changed.</param>
        /// <param name="oldPassword">The current password as a <see cref="SecureString"/>.</param>
        /// <param name="newPassword">The new password as a <see cref="SecureString"/>.</param>
        public void ChangePassword(CSEntry csentry, SecureString oldPassword, SecureString newPassword)
        {
            CSEntryIdentity identity = RealCSEntryToIdentity.Read(csentry);
            ChangePasswordCore(identity, oldPassword, newPassword);
        }

        // -------------------------------------------------------------------------
        // Internal testable seams
        // -------------------------------------------------------------------------

        /// <summary>
        /// Serialises the identity, converts <paramref name="newPassword"/> to plaintext at the wire
        /// boundary, and sends the <c>SetPassword</c> RPC to the worker.
        /// </summary>
        /// <remarks>
        /// SECURITY: the plaintext buffer allocated by <see cref="SecureStringConverter.ToPlainText"/> is
        /// zeroed by that method in its own <c>finally</c> block; the managed string copy is a local scoped
        /// to this method and goes out of scope immediately after the RPC call. This method is
        /// <c>internal</c> so tests can invoke it directly without a live worker.
        /// </remarks>
        /// <param name="identity">The extracted identity for the target object.</param>
        /// <param name="newPassword">The new password as a <see cref="SecureString"/>.</param>
        /// <param name="options">Password-change option flags.</param>
        /// <exception cref="ExtensibleExtensionException">Thrown when the worker reports a failure.</exception>
        internal void SetPasswordCore(
            CSEntryIdentity identity,
            SecureString newPassword,
            PasswordOptions options)
        {
            string identityXml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(identity);

            // Convert to plaintext only at the wire boundary. The unmanaged buffer is zeroed by ToPlainText
            // in its finally block. The local 'plain' goes out of scope at the end of this method.
            string plain = SecureStringConverter.ToPlainText(newPassword);

            // A provider failure now propagates as the EXACT real host exception (Path C exception marshalling):
            // the pipe client reconstructs and throws the host type the worker threw (e.g.
            // OldPasswordIncorrectException, PasswordPolicyViolationException), or an ExtensibleExtensionException
            // carrier for a non-host worker exception. Those flow straight to MIM. Only an envelope-less
            // transport/framing failure surfaces as InvalidOperationException; convert THAT to a host exception
            // so MIM still handles it (fail-closed). The worker message is secret-free by construction.
            try
            {
                this.session.Client.SetPassword(identityXml, plain, options.ToString());
            }
            catch (InvalidOperationException ex)
            {
                throw new ExtensibleExtensionException(ex.Message);
            }
        }

        /// <summary>
        /// Serialises the identity, converts <paramref name="oldPassword"/> and <paramref name="newPassword"/>
        /// to plaintext at the wire boundary, and sends the <c>ChangePassword</c> RPC to the worker.
        /// </summary>
        /// <remarks>
        /// SECURITY: each <see cref="SecureString"/>'s unmanaged buffer is zeroed by
        /// <see cref="SecureStringConverter.ToPlainText"/> in its own <c>finally</c> block; the managed
        /// string copies are locals not retained beyond the point of use. This method is <c>internal</c> so
        /// tests can invoke it directly without a live worker.
        /// </remarks>
        /// <param name="identity">The extracted identity for the target object.</param>
        /// <param name="oldPassword">The current password as a <see cref="SecureString"/>.</param>
        /// <param name="newPassword">The new password as a <see cref="SecureString"/>.</param>
        /// <exception cref="ExtensibleExtensionException">Thrown when the worker reports a failure.</exception>
        internal void ChangePasswordCore(
            CSEntryIdentity identity,
            SecureString oldPassword,
            SecureString newPassword)
        {
            string identityXml = MmsPipeSerializer.SerializeXml<CSEntryIdentity>(identity);

            // Convert each SecureString to plaintext only at the wire boundary. Both unmanaged buffers are
            // zeroed by ToPlainText in their respective finally blocks. The locals go out of scope at the
            // end of this method.
            string plainOld = SecureStringConverter.ToPlainText(oldPassword);
            string plainNew = SecureStringConverter.ToPlainText(newPassword);

            // A provider failure now propagates as the EXACT real host exception (Path C exception marshalling);
            // only an envelope-less transport/framing failure surfaces as InvalidOperationException, which is
            // converted to a host exception so MIM still handles it (fail-closed). Secret-free by construction.
            try
            {
                this.session.Client.ChangePassword(identityXml, plainOld, plainNew);
            }
            catch (InvalidOperationException ex)
            {
                throw new ExtensibleExtensionException(ex.Message);
            }
        }

    }
}
