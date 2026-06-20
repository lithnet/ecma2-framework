using System;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Runtime.Serialization.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using Lithnet.Ecma2Framework.Serialization;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// A BCL-only JSON-RPC 2.0 client that communicates over a named pipe using
    /// Content-Length header-delimited framing, matching the transport emitted by
    /// StreamJsonRpc's <c>HeaderDelimitedMessageHandler</c> on the worker side.
    /// </summary>
    /// <remarks>
    /// Transport (Path C): every method's real <c>Microsoft.MetadirectoryServices</c>-typed
    /// payload crosses as a <c>MmsPipeSerializer</c> XML string carried as a JSON-RPC string
    /// argument or string result. Simple scalars (enum names, page sizes, custom-data) cross as
    /// plain JSON string/int arguments. There is no structured wire DTO layer; the worker
    /// re-materialises the real graph and the shim hands the real graph straight to the host.
    ///
    /// Framing contract (confirmed against StreamJsonRpc HeaderDelimitedMessageHandler source, 2026-06):
    /// <list type="bullet">
    ///   <item>WRITE: ASCII header <c>Content-Length: &lt;N&gt;\r\n\r\n</c> followed by the N-byte UTF-8 (no BOM) payload.</item>
    ///   <item>READ: one or more ASCII header lines terminated by <c>\r\n</c>, then an empty line (<c>\r\n</c> alone),
    ///     then exactly <c>Content-Length</c> bytes decoded as UTF-8 (no BOM).  All headers other than
    ///     <c>Content-Length</c> are ignored (StreamJsonRpc also emits <c>Content-Type</c>).</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item>A JSON-RPC <c>error</c> response from the worker throws <see cref="InvalidOperationException"/>
    ///     with the worker-supplied message.</item>
    ///   <item>A malformed frame (missing <c>Content-Length</c>, non-numeric value, header-only response
    ///     with no body) throws <see cref="InvalidOperationException"/>.</item>
    ///   <item><see cref="WaitForConnection"/> throws <see cref="TimeoutException"/> when no worker
    ///     connects within the specified timeout.</item>
    /// </list>
    ///
    /// SECURITY (same-identity gate): passwords cross this pipe as plaintext, so the channel is restricted to
    /// a single identity by two layers. (1) The pipe ACL grants access to ONLY the creating user's SID
    /// (<see cref="CreateCurrentUserOnlyPipeSecurity"/>), so the OS denies any other account at connect time.
    /// (2) After a client connects, <see cref="VerifyConnectedClientIsSameIdentity"/> reads the client's SID
    /// and fails closed unless it equals the server's. A rogue process squatting the pipe name is prevented
    /// by the unique per-session GUID name + creating the server before the worker is spawned + a single
    /// allowed instance, so the worker reaches only the genuine host server.
    /// Residual: the worker does not additionally read the server's owner SID (that needs the Windows-only
    /// pipe-ACL APIs, which are unavailable on the worker's plain net8 target); the squat-prevention above
    /// covers that direction.
    /// </remarks>
    internal sealed class JsonRpcPipeClient : IDisposable
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly NamedPipeServerStream pipeServer;
        private int nextRequestId = 1;
        private bool disposed;

        /// <param name="pipeName">The name of the pipe to create.</param>
        public JsonRpcPipeClient(string pipeName)
        {
            if (pipeName == null)
            {
                throw new ArgumentNullException("pipeName");
            }

            // SECURITY: restrict the pipe ACL to ONLY the identity that creates it (the host service
            // account). The OS then denies every other account the right to connect at all — the primary,
            // kernel-enforced same-identity control for a channel that carries passwords in plaintext.
            // maxNumberOfServerInstances = 1, plus creating the server here (before the worker is spawned),
            // also prevents a rogue process from squatting the unique pipe name with a competing server.
            PipeSecurity pipeSecurity = CreateCurrentUserOnlyPipeSecurity();

            this.pipeServer = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                0,
                0,
                pipeSecurity);
        }

        /// <summary>
        /// Waits for the worker process to connect to the pipe.
        /// </summary>
        /// <param name="timeoutMs">Maximum milliseconds to wait.</param>
        /// <exception cref="TimeoutException">No client connected within the timeout.</exception>
        public void WaitForConnection(int timeoutMs)
        {
            IAsyncResult ar = this.pipeServer.BeginWaitForConnection(null, null);

            bool connected = ar.AsyncWaitHandle.WaitOne(timeoutMs);

            if (!connected)
            {
                throw new TimeoutException(
                    string.Format("Worker process did not connect to pipe within {0} ms.", timeoutMs));
            }

            this.pipeServer.EndWaitForConnection(ar);

            this.VerifyConnectedClientIsSameIdentity();
        }

        /// <summary>
        /// Builds a pipe ACL that grants full control to ONLY the current user's SID. With no other access
        /// rules, the OS denies every other account, so only a same-identity process can open the pipe.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the current process identity has no user SID, so the pipe cannot be secured to a single
        /// identity. Failing here is correct — an unsecurable pipe must not be created for a secret-bearing
        /// channel.
        /// </exception>
        internal static PipeSecurity CreateCurrentUserOnlyPipeSecurity()
        {
            SecurityIdentifier currentUser;

            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                currentUser = identity.User;
            }

            if (currentUser == null)
            {
                throw new InvalidOperationException(
                    "The current process identity has no user SID, so the worker pipe cannot be secured to a single identity.");
            }

            PipeSecurity security = new PipeSecurity();
            security.SetOwner(currentUser);
            security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.FullControl, AccessControlType.Allow));
            return security;
        }

        /// <summary>
        /// Verifies that the process which connected to the pipe runs as the SAME identity as this host, and
        /// fails closed (disconnects and throws) on any mismatch: a worker that is not same-identity must never
        /// receive what crosses this pipe, because passwords cross it in plaintext.
        /// </summary>
        /// <remarks>
        /// This is defence-in-depth on top of the pipe ACL, which the OS already enforces at connect time. It
        /// requires the client to have connected with at least <see cref="TokenImpersonationLevel.Identification"/>
        /// (the worker does), so the server can read the client's identity via
        /// <see cref="NamedPipeServerStream.RunAsClient"/> without being able to act as it.
        /// </remarks>
        /// <exception cref="UnauthorizedAccessException">Thrown when the connected client is not same-identity.</exception>
        private void VerifyConnectedClientIsSameIdentity()
        {
            SecurityIdentifier serverUser;

            using (WindowsIdentity serverIdentity = WindowsIdentity.GetCurrent())
            {
                serverUser = serverIdentity.User;
            }

            SecurityIdentifier clientUser = null;

            this.pipeServer.RunAsClient(() =>
            {
                using (WindowsIdentity clientIdentity = WindowsIdentity.GetCurrent())
                {
                    clientUser = clientIdentity.User;
                }
            });

            if (serverUser == null || clientUser == null || !clientUser.Equals(serverUser))
            {
                try
                {
                    this.pipeServer.Disconnect();
                }
                catch (Exception)
                {
                    // Best-effort: the connection is being refused regardless; the throw below is what matters.
                }

                throw new UnauthorizedAccessException(
                    "The process that connected to the worker pipe does not run as the same identity as the host. " +
                    "The connection was refused because secrets cross this channel in plaintext and must only reach " +
                    "a same-identity worker.");
            }
        }

        // -------------------------------------------------------------------------
        // Schema / capabilities / config-parameter initialisation methods
        // Each carries the real MMS payload as a MmsPipeSerializer XML string.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends <c>GetSchema</c> and returns the real schema as a <c>MmsPipeSerializer</c> XML string.
        /// </summary>
        public string GetSchema(string configParametersXml)
        {
            return this.InvokeStringResult("GetSchema", StringArg(configParametersXml));
        }

        /// <summary>
        /// Sends <c>GetCapabilities</c> with the serialised config-parameter list and returns the
        /// real <c>MACapabilities</c> as a <c>MmsPipeSerializer</c> XML string.
        /// </summary>
        public string GetCapabilities(string configParametersXml)
        {
            return this.InvokeStringResult("GetCapabilities", StringArg(configParametersXml));
        }

        /// <summary>
        /// Sends <c>GetConfigParameters</c> and returns the serialised definition list.
        /// </summary>
        public string GetConfigParameters(string configParametersXml, string page, int pageNumber)
        {
            return this.InvokeStringResult(
                "GetConfigParameters",
                StringArg(configParametersXml),
                StringArg(page),
                IntArg(pageNumber));
        }

        /// <summary>
        /// Sends <c>ValidateConfigParameters</c> and returns the real <c>ParameterValidationResult</c>
        /// as a <c>MmsPipeSerializer</c> XML string.
        /// </summary>
        public string ValidateConfigParameters(string configParametersXml, string page, int pageNumber)
        {
            return this.InvokeStringResult(
                "ValidateConfigParameters",
                StringArg(configParametersXml),
                StringArg(page),
                IntArg(pageNumber));
        }

        // -------------------------------------------------------------------------
        // Import session methods
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends <c>OpenImport</c> with the serialised run-step and schema; returns the open-time
        /// custom-data string (null until close).
        /// </summary>
        public string OpenImport(string runStepXml, string schemaXml, string configParametersXml, string extensionsDirectory)
        {
            return this.InvokeStringResult(
                "OpenImport",
                StringArg(runStepXml),
                StringArg(schemaXml),
                StringArg(configParametersXml),
                StringArg(extensionsDirectory));
        }

        /// <summary>
        /// Sends <c>GetImportPage</c> and returns the page: the serialised entry list plus the
        /// paging scalars.
        /// </summary>
        public ImportPageResult GetImportPage()
        {
            byte[] responseBytes = this.Invoke("GetImportPage");

            ImportPageResultEnvelope envelope = ParseEnvelope<ImportPageResultEnvelope>(responseBytes, "GetImportPage");

            ImportPageResultData data = envelope.Result ?? new ImportPageResultData();

            return new ImportPageResult
            {
                EntriesXml = data.EntriesXml,
                MoreToImport = data.MoreToImport,
                CustomData = data.CustomData,
            };
        }

        /// <summary>
        /// Sends <c>CloseImport</c> with the inbound custom-data; returns the outbound watermark.
        /// </summary>
        public string CloseImport(string customData)
        {
            return this.InvokeStringResult("CloseImport", StringArg(customData));
        }

        // -------------------------------------------------------------------------
        // Export session methods
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends <c>OpenExport</c> with the serialised run-step and schema.
        /// </summary>
        public void OpenExport(string runStepXml, string schemaXml, string configParametersXml, string extensionsDirectory)
        {
            this.InvokeBoolResult(
                "OpenExport",
                StringArg(runStepXml),
                StringArg(schemaXml),
                StringArg(configParametersXml),
                StringArg(extensionsDirectory));
        }

        /// <summary>
        /// Sends <c>PutExport</c> with the serialised entry batch; returns the serialised result list.
        /// </summary>
        public string PutExport(string entriesXml)
        {
            return this.InvokeStringResult("PutExport", StringArg(entriesXml));
        }

        /// <summary>
        /// Sends <c>CloseExport</c>.
        /// </summary>
        public void CloseExport()
        {
            this.InvokeBoolResult("CloseExport");
        }

        // -------------------------------------------------------------------------
        // Password session methods
        //
        // The identity object crosses as a CSEntryIdentity MmsPipeSerializer XML string. Secrets cross as
        // plain JSON string arguments over the same-identity local pipe; this client never logs them.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends <c>OpenPassword</c> with the serialised config-parameter list and partition.
        /// </summary>
        public void OpenPassword(string configParametersXml, string partitionXml, string extensionsDirectory)
        {
            this.InvokeBoolResult(
                "OpenPassword",
                StringArg(configParametersXml),
                StringArg(partitionXml),
                StringArg(extensionsDirectory));
        }

        /// <summary>
        /// Sends <c>GetSecurityLevel</c> and returns the connection security level name.
        /// </summary>
        public string GetSecurityLevel()
        {
            return this.InvokeStringResult("GetSecurityLevel");
        }

        /// <summary>
        /// Sends <c>SetPassword</c> with the serialised identity, the new-password plaintext, and the
        /// password-options name.
        /// </summary>
        /// <remarks>SECURITY: <paramref name="newPassword"/> is a secret; it is never logged.</remarks>
        public void SetPassword(string identityXml, string newPassword, string options)
        {
            this.InvokeBoolResult("SetPassword", StringArg(identityXml), StringArg(newPassword), StringArg(options));
        }

        /// <summary>
        /// Sends <c>ChangePassword</c> with the serialised identity and both password plaintexts.
        /// </summary>
        /// <remarks>SECURITY: both passwords are secrets; they are never logged.</remarks>
        public void ChangePassword(string identityXml, string oldPassword, string newPassword)
        {
            this.InvokeBoolResult("ChangePassword", StringArg(identityXml), StringArg(oldPassword), StringArg(newPassword));
        }

        /// <summary>
        /// Sends <c>ClosePassword</c>.
        /// </summary>
        public void ClosePassword()
        {
            this.InvokeBoolResult("ClosePassword");
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;

            this.pipeServer.Dispose();
        }

        // -------------------------------------------------------------------------
        // Argument encoding
        // -------------------------------------------------------------------------

        /// <summary>
        /// Encodes a string argument as a JSON value (a JSON string, or the literal <c>null</c>).
        /// </summary>
        private static string StringArg(string value)
        {
            if (value == null)
            {
                return "null";
            }

            return JsonEscape(value);
        }

        /// <summary>
        /// Encodes an integer argument as a JSON number literal.
        /// </summary>
        private static string IntArg(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Escapes a string and wraps it in double quotes to form a JSON string literal.
        /// </summary>
        private static string JsonEscape(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length + 2);
            sb.Append('"');

            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
            return sb.ToString();
        }

        // -------------------------------------------------------------------------
        // Request dispatch
        // -------------------------------------------------------------------------

        /// <summary>
        /// Sends a JSON-RPC request whose <c>params</c> array holds the supplied raw JSON argument
        /// values, and returns the raw response frame bytes.
        /// </summary>
        private byte[] Invoke(string method, params string[] jsonArgs)
        {
            int id = this.nextRequestId++;

            string paramsArray = jsonArgs == null || jsonArgs.Length == 0
                ? string.Empty
                : string.Join(",", jsonArgs);

            string requestJson = string.Format(
                CultureInfo.InvariantCulture,
                "{{\"jsonrpc\":\"2.0\",\"id\":{0},\"method\":\"{1}\",\"params\":[{2}]}}",
                id,
                method,
                paramsArray);

            WriteFrame(Utf8NoBom.GetBytes(requestJson));

            return ReadFrame();
        }

        /// <summary>
        /// Sends a request and returns its <c>result</c> as a string.
        /// </summary>
        private string InvokeStringResult(string method, params string[] jsonArgs)
        {
            byte[] responseBytes = this.Invoke(method, jsonArgs);
            StringResultEnvelope envelope = ParseEnvelope<StringResultEnvelope>(responseBytes, method);
            return envelope.Result;
        }

        /// <summary>
        /// Sends a request whose <c>result</c> is a boolean acknowledgement, ignoring the value
        /// (errors still propagate via the envelope's <c>error</c> member).
        /// </summary>
        private void InvokeBoolResult(string method, params string[] jsonArgs)
        {
            byte[] responseBytes = this.Invoke(method, jsonArgs);
            ParseEnvelope<BoolResultEnvelope>(responseBytes, method);
        }

        // -------------------------------------------------------------------------
        // Frame write
        // -------------------------------------------------------------------------

        /// <summary>
        /// Writes a Content-Length-framed message to the pipe.
        /// Format: <c>Content-Length: N\r\n\r\n</c> followed by <paramref name="payload"/>.
        /// </summary>
        private void WriteFrame(byte[] payload)
        {
            string header = "Content-Length: " + payload.Length + "\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);

            this.pipeServer.Write(headerBytes, 0, headerBytes.Length);
            this.pipeServer.Write(payload, 0, payload.Length);
            this.pipeServer.Flush();
        }

        // -------------------------------------------------------------------------
        // Frame read
        // -------------------------------------------------------------------------

        /// <summary>
        /// Reads a Content-Length-framed message from the pipe.
        /// </summary>
        private byte[] ReadFrame()
        {
            int contentLength = ReadHeaders();

            if (contentLength < 0)
            {
                throw new InvalidOperationException(
                    "JSON-RPC framing error: response did not include a Content-Length header.");
            }

            return ReadExactBytes(contentLength);
        }

        /// <summary>
        /// Reads header lines from the pipe until an empty line is encountered.
        /// Returns the value of the <c>Content-Length</c> header (case-insensitive match), or -1.
        /// </summary>
        private int ReadHeaders()
        {
            int contentLength = -1;

            while (true)
            {
                string line = ReadAsciiLine();

                if (line.Length == 0)
                {
                    break;
                }

                int colon = line.IndexOf(':');

                if (colon < 0)
                {
                    continue;
                }

                string name = line.Substring(0, colon).Trim();
                string value = line.Substring(colon + 1).Trim();

                if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, out contentLength))
                    {
                        throw new InvalidOperationException(
                            string.Format(
                                "JSON-RPC framing error: Content-Length value '{0}' is not a valid integer.",
                                value));
                    }
                }
            }

            return contentLength;
        }

        /// <summary>
        /// Reads one header line (ASCII), stripping the trailing <c>\r</c>.
        /// </summary>
        private string ReadAsciiLine()
        {
            StringBuilder sb = new StringBuilder();

            while (true)
            {
                int b = this.pipeServer.ReadByte();

                if (b == -1)
                {
                    throw new InvalidOperationException(
                        "JSON-RPC framing error: stream ended unexpectedly while reading headers.");
                }

                char ch = (char)b;

                if (ch == '\n')
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] == '\r')
                    {
                        sb.Remove(sb.Length - 1, 1);
                    }

                    return sb.ToString();
                }

                sb.Append(ch);
            }
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes from the pipe.
        /// </summary>
        private byte[] ReadExactBytes(int count)
        {
            byte[] buffer = new byte[count];
            int offset = 0;

            while (offset < count)
            {
                int read = this.pipeServer.Read(buffer, offset, count - offset);

                if (read == 0)
                {
                    throw new InvalidOperationException(
                        string.Format(
                            "JSON-RPC framing error: stream ended after {0} of {1} expected body bytes.",
                            offset,
                            count));
                }

                offset += read;
            }

            return buffer;
        }

        // -------------------------------------------------------------------------
        // Response parsing
        // -------------------------------------------------------------------------

        /// <summary>
        /// Deserialises a JSON-RPC 2.0 response envelope of type <typeparamref name="TEnvelope"/>.
        /// A non-null <c>error</c> member throws <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <typeparam name="TEnvelope">A <c>[DataContract]</c> envelope with an <c>Error</c> member.</typeparam>
        /// <param name="responseBytes">Raw response frame bytes.</param>
        /// <param name="methodName">The method name, used only in exception messages.</param>
        private static TEnvelope ParseEnvelope<TEnvelope>(byte[] responseBytes, string methodName)
            where TEnvelope : RpcResponseEnvelope
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TEnvelope));

            TEnvelope envelope;

            using (MemoryStream ms = new MemoryStream(responseBytes))
            {
                envelope = (TEnvelope)serializer.ReadObject(ms);
            }

            if (envelope == null)
            {
                throw new InvalidOperationException(
                    string.Format("JSON-RPC response for '{0}' deserialisation returned null.", methodName));
            }

            if (envelope.Error != null)
            {
                ThrowWorkerError(envelope.Error, methodName);
            }

            return envelope;
        }

        /// <summary>
        /// Converts a JSON-RPC <c>error</c> object into the exception the host should see. When the error
        /// carries a structured <c>MmsExceptionEnvelope</c> (Path C), the EXACT real host exception is
        /// reconstructed and thrown so FIM's type-driven handling fires; otherwise a transport/framing error
        /// surfaces as an <see cref="InvalidOperationException"/> with the worker-supplied message.
        /// </summary>
        private static void ThrowWorkerError(RpcError error, string methodName)
        {
            if (!string.IsNullOrEmpty(error.Data))
            {
                MmsExceptionEnvelope envelope = MmsExceptionEnvelopeSerializer.Deserialize(error.Data);

                if (envelope != null)
                {
                    // Reconstructs and throws the exact host exception (or the ExtensibleExtensionException
                    // carrier for a non-host worker exception). Never returns.
                    MmsExceptionReconstructor.Throw(envelope);
                }
            }

            // No structured envelope: a transport/framing-level failure. Surface the worker message verbatim.
            throw new InvalidOperationException(
                string.Format(
                    "Worker returned JSON-RPC error for '{0}' (code {1}): {2}",
                    methodName,
                    error.Code,
                    error.Message));
        }
    }
}
