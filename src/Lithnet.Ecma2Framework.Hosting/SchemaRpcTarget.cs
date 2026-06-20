using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Lithnet.Ecma2Framework.Internal;
using Lithnet.Ecma2Framework.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.MetadirectoryServices;
using StreamJsonRpc;

namespace Lithnet.Ecma2Framework.Hosting
{
    /// <summary>
    /// The JSON-RPC target object registered with StreamJsonRpc. StreamJsonRpc maps incoming
    /// request method names to methods on this class by convention (e.g. <c>GetSchema</c> →
    /// <see cref="GetSchema"/>).
    ///
    /// One instance of this class is created per worker process and lives for the duration of
    /// the JSON-RPC session. It holds a single <see cref="Ecma2ImportOrchestrator"/> that is
    /// created on <see cref="OpenImport"/> and discarded on <see cref="CloseImport"/>, a single
    /// <see cref="Ecma2ExportOrchestrator"/> scoped to the export run, and a single
    /// <see cref="Ecma2PasswordOrchestrator"/> scoped to the password-management run.
    /// </summary>
    /// <remarks>
    /// Transport (Path C): every method's real <c>Microsoft.MetadirectoryServices</c>-typed
    /// parameter(s) and result cross the pipe as a <see cref="MmsPipeSerializer"/> XML string
    /// (a DataContract serialisation of the real graph via the shared surrogate). Simple scalars
    /// (enum names, page sizes, watermark custom-data) cross as plain JSON-RPC string/int args.
    /// No mirror types are involved.
    ///
    /// The <paramref name="workerHost"/> is built by the consumer-hosted entry point before the serve
    /// loop starts; it is non-null in normal operation. If it is ever absent, import/export/config
    /// operations throw <see cref="InvalidOperationException"/> with a clear message.
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenImport"/>/<see cref="OpenExport"/>/the config methods throw
    ///     <see cref="InvalidOperationException"/> when no consumer was loaded (workerHost is null).</item>
    ///   <item><see cref="GetImportPage"/> throws <see cref="InvalidOperationException"/> when
    ///     called before <see cref="OpenImport"/>.</item>
    ///   <item>Handler failures propagate as JSON-RPC errors to the shim.</item>
    /// </list>
    /// </remarks>
    internal class SchemaRpcTarget
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        // Guarded by the single-threaded JSON-RPC dispatch model — StreamJsonRpc dispatches
        // requests sequentially on the JSON-RPC thread, so no locking is needed here.
        private Ecma2ImportOrchestrator _importOrchestrator;
        private int _importPageSize;

        // Export orchestrator state is kept separate from import state; a single worker process
        // handles one connection and therefore at most one import and one export session.
        private Ecma2ExportOrchestrator _exportOrchestrator;

        // Password orchestrator state is kept separate from import and export state; a single worker
        // process handles at most one password session. Created on OpenPassword, discarded on ClosePassword.
        //
        // Transport (Path C): the password path carries our own CSEntryIdentity (NOT a host CSEntry, which
        // is an abstract live-engine object with no constructible form) as a MmsPipeSerializer XML string.
        // The shim extracts the identity + present attribute values from the real CSEntry on net48; the
        // worker deserialises it and hands it to the provider. Secrets cross as plain string arguments over
        // the same-identity local pipe and are wrapped in a SecureString immediately, disposed in a finally
        // block, and NEVER logged or placed in an exception message.
        private Ecma2PasswordOrchestrator _passwordOrchestrator;

        // The WorkerHost holding the consumer DI container, built by the consumer-hosted entry point.
        private readonly WorkerHost _workerHost;

        /// <summary>
        /// Runs a synchronous handler, converting any thrown exception into a JSON-RPC error whose
        /// <c>data</c> member carries a faithful <see cref="MmsExceptionEnvelope"/> (Path C). The shim
        /// reconstructs and re-throws the EXACT host exception (or an <c>ExtensibleExtensionException</c>
        /// carrier for a non-host worker exception), so FIM's type-driven handling fires.
        /// </summary>
        /// <remarks>
        /// SECURITY: the password handlers construct their provider exceptions secret-free by design; the
        /// envelope copies only the type name, the host-declared diagnostic fields, the (secret-free) message,
        /// and the inner chain. No secret is read into it.
        /// </remarks>
        private static T Guard<T>(Func<T> handler)
        {
            try
            {
                return handler();
            }
            catch (Exception ex)
            {
                throw ToRpcException(ex);
            }
        }

        /// <summary>
        /// The asynchronous counterpart of <see cref="Guard{T}"/>.
        /// </summary>
        private static async Task<T> GuardAsync<T>(Func<Task<T>> handler)
        {
            try
            {
                return await handler();
            }
            catch (Exception ex)
            {
                throw ToRpcException(ex);
            }
        }

        /// <summary>
        /// Wraps an exception in a <see cref="LocalRpcException"/> carrying the serialised
        /// <see cref="MmsExceptionEnvelope"/> as the JSON-RPC error <c>data</c> member.
        /// </summary>
        private static LocalRpcException ToRpcException(Exception ex)
        {
            MmsExceptionEnvelope envelope = MmsExceptionEnvelopeFactory.FromException(ex);
            string envelopeJson = MmsExceptionEnvelopeSerializer.Serialize(envelope);

            // The Message carried on the LocalRpcException is the human-readable fallback the shim uses when no
            // structured envelope is present; it is secret-free (the password handlers guarantee this).
            return new LocalRpcException(ex.Message)
            {
                ErrorCode = (int)StreamJsonRpc.Protocol.JsonRpcErrorCode.InvocationError,
                ErrorData = envelopeJson,
            };
        }

        /// <summary>
        /// Initialises a <see cref="SchemaRpcTarget"/> with an optional worker host.
        /// </summary>
        /// <param name="workerHost">
        /// The consumer host built by the consumer-hosted entry point.
        /// </param>
        internal SchemaRpcTarget(WorkerHost workerHost)
        {
            _workerHost = workerHost;
        }

        /// <summary>
        /// Handles the <c>GetSchema</c> JSON-RPC request and returns the real schema as a
        /// <see cref="MmsPipeSerializer"/> XML string.
        /// </summary>
        public Task<string> GetSchema(string configParametersXml)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "GetSchema requires the consumer host, which was not initialised.");
                }

                // Schema discovery is not parameter-independent: a provider may need the connectivity
                // parameters to enumerate the target system's schema. Build with the real parameters.
                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);
                Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(_workerHost.Services);
                Schema schema = await orchestrator.GetSchemaAsync();
                return MmsPipeSerializer.SerializeXml<Schema>(schema);
            });
        }

        /// <summary>
        /// Handles the <c>GetCapabilities</c> JSON-RPC request.
        /// </summary>
        /// <param name="configParametersXml">
        /// The current configuration parameters as a serialised <see cref="List{T}"/> of real
        /// <see cref="ConfigParameter"/>.
        /// </param>
        /// <returns>The real <see cref="MACapabilities"/> as a <see cref="MmsPipeSerializer"/> XML string.</returns>
        public Task<string> GetCapabilities(string configParametersXml)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "GetCapabilities requires the consumer host, which was not initialised.");
                }

                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);

                IConfigParameters configParameters = _workerHost.Services.GetService<IConfigParameters>();
                Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(_workerHost.Services);

                MACapabilities capabilities = await orchestrator.GetCapabilitiesAsync(configParameters);

                return MmsPipeSerializer.SerializeXml<MACapabilities>(capabilities);
            });
        }

        /// <summary>
        /// Handles the <c>GetConfigParameters</c> JSON-RPC request.
        /// </summary>
        /// <param name="configParametersXml">Existing config params (serialised <see cref="List{T}"/> of <see cref="ConfigParameter"/>).</param>
        /// <param name="page">The <see cref="ConfigParameterPage"/> name.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <returns>The definition list (serialised <see cref="List{T}"/> of <see cref="ConfigParameterDefinition"/>).</returns>
        public Task<string> GetConfigParameters(string configParametersXml, string page, int pageNumber)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "GetConfigParameters requires the consumer host, which was not initialised.");
                }

                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);

                IConfigParameters configParameters = _workerHost.Services.GetService<IConfigParameters>();
                ConfigParameterPage configPage = (ConfigParameterPage)Enum.Parse(typeof(ConfigParameterPage), page);
                Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(_workerHost.Services);

                IList<ConfigParameterDefinition> definitions =
                    await orchestrator.GetConfigParametersAsync(configParameters, configPage, pageNumber);

                List<ConfigParameterDefinition> list = new List<ConfigParameterDefinition>(definitions);
                return MmsPipeSerializer.SerializeXml<List<ConfigParameterDefinition>>(list);
            });
        }

        /// <summary>
        /// Handles the <c>ValidateConfigParameters</c> JSON-RPC request.
        /// </summary>
        /// <param name="configParametersXml">Config params (serialised <see cref="List{T}"/> of <see cref="ConfigParameter"/>).</param>
        /// <param name="page">The <see cref="ConfigParameterPage"/> name.</param>
        /// <param name="pageNumber">The page number.</param>
        /// <returns>The real <see cref="ParameterValidationResult"/> as a <see cref="MmsPipeSerializer"/> XML string.</returns>
        public Task<string> ValidateConfigParameters(string configParametersXml, string page, int pageNumber)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "ValidateConfigParameters requires the consumer host, which was not initialised.");
                }

                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);

                IConfigParameters configParameters = _workerHost.Services.GetService<IConfigParameters>();
                ConfigParameterPage configPage = (ConfigParameterPage)Enum.Parse(typeof(ConfigParameterPage), page);
                Ecma2InitOrchestrator orchestrator = new Ecma2InitOrchestrator(_workerHost.Services);

                ParameterValidationResult validationResult =
                    await orchestrator.ValidateConfigParametersAsync(configParameters, configPage, pageNumber);

                return MmsPipeSerializer.SerializeXml<ParameterValidationResult>(validationResult);
            });
        }

        /// <summary>
        /// Handles the <c>OpenImport</c> JSON-RPC request.
        /// </summary>
        /// <param name="runStepXml">The real <see cref="OpenImportConnectionRunStep"/> as XML.</param>
        /// <param name="schemaXml">The real <see cref="Schema"/> as XML.</param>
        /// <param name="extensionsDirectory">
        /// The host's <c>Utils.ExtensionsDirectory</c>, captured engine-side by the shim and injected here so
        /// providers can read it via <see cref="IEngineServices"/> instead of the worker's own (wrong) static.
        /// May be null.
        /// </param>
        /// <returns>The outbound custom-data string (null until <see cref="CloseImport"/> runs).</returns>
        public Task<string> OpenImport(string runStepXml, string schemaXml, string configParametersXml, string extensionsDirectory)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "OpenImport requires the consumer host, which was not initialised.");
                }

                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);

                OpenImportConnectionRunStep runStep =
                    MmsPipeSerializer.DeserializeXml<OpenImportConnectionRunStep>(runStepXml);
                Schema schema = MmsPipeSerializer.DeserializeXml<Schema>(schemaXml);

                InjectExtensionsDirectory(extensionsDirectory);

                ImportContext context = ImportContext.Create(runStep, schema);

                // Deserialise the incoming watermark from CustomData if present.
                // The Create factory initialises IncomingWatermark to an empty dict;
                // overwrite it here only when a non-empty JSON watermark is supplied.
                if (!string.IsNullOrEmpty(runStep.CustomData))
                {
                    try
                    {
                        System.Collections.Concurrent.ConcurrentDictionary<string, string> incomingWatermark =
                            JsonSerializer.Deserialize<System.Collections.Concurrent.ConcurrentDictionary<string, string>>(
                                runStep.CustomData,
                                JsonOptions);

                        context.IncomingWatermark = incomingWatermark;
                    }
                    catch (JsonException ex)
                    {
                        // Watermark is not JSON — leave the empty dict so a full import proceeds. Log the
                        // discarded parse error (the watermark is not secret) so a genuinely corrupt watermark
                        // is diagnosable rather than silently degrading to a full import.
                        Console.Error.WriteLine(
                            "[worker] OpenImport: incoming watermark was not valid JSON; proceeding as a full import. " + ex.Message);
                    }
                }

                _importPageSize = runStep.PageSize > 0 ? runStep.PageSize : 100;

                _importOrchestrator = new Ecma2ImportOrchestrator(_workerHost.Services);

                await _importOrchestrator.OpenAsync(context);

                // Watermarks are returned in CloseImport; OpenImport reports no custom data.
                return (string)null;
            });
        }

        /// <summary>
        /// Handles the <c>GetImportPage</c> JSON-RPC request.
        /// Drains up to the negotiated page size from the orchestrator and returns the page.
        /// </summary>
        /// <returns>An <see cref="ImportPageResult"/> carrying the serialised entry list,
        /// the <c>MoreToImport</c> flag, and the page custom-data.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown (and surfaced as a JSON-RPC error) when this method is called before
        /// <see cref="OpenImport"/> has established an import session.
        /// </exception>
        public Task<ImportPageResult> GetImportPage()
        {
            return GuardAsync(async () =>
            {
                if (_importOrchestrator == null)
                {
                    throw new InvalidOperationException(
                        "GetImportPage was called before OpenImport. Call OpenImport first to establish an import session.");
                }

                ImportPage page = await _importOrchestrator.GetNextPageAsync(
                    _importPageSize,
                    CancellationToken.None);

                List<CSEntryChange> entries = new List<CSEntryChange>(page.Entries);

                return new ImportPageResult
                {
                    EntriesXml = MmsPipeSerializer.SerializeXml<List<CSEntryChange>>(entries),
                    MoreToImport = page.MoreToImport,
                    CustomData = null,
                };
            });
        }

        /// <summary>
        /// Handles the <c>CloseImport</c> JSON-RPC request.
        /// Awaits the producer to finish, retrieves the outbound watermark, and discards the session.
        /// </summary>
        /// <param name="customData">The inbound custom-data echoed back when no session is active.</param>
        /// <returns>The serialised outbound watermark, or the echoed custom-data.</returns>
        public Task<string> CloseImport(string customData)
        {
            return GuardAsync(async () =>
            {
                if (_importOrchestrator == null)
                {
                    // Idempotent: if CloseImport is called without a prior OpenImport (or after a
                    // previous CloseImport), echo the inbound custom-data rather than throwing.
                    return customData;
                }

                string watermark = await _importOrchestrator.CloseAsync();
                _importOrchestrator = null;

                return string.IsNullOrEmpty(watermark) ? null : watermark;
            });
        }

        /// <summary>
        /// Handles the <c>OpenExport</c> JSON-RPC request.
        /// </summary>
        /// <param name="runStepXml">The real <see cref="OpenExportConnectionRunStep"/> as XML.</param>
        /// <param name="schemaXml">The real <see cref="Schema"/> as XML (may be empty).</param>
        /// <param name="extensionsDirectory">
        /// The host's <c>Utils.ExtensionsDirectory</c>, captured engine-side by the shim and injected here so
        /// providers can read it via <see cref="IEngineServices"/>. May be null.
        /// </param>
        /// <returns><c>true</c> on success.</returns>
        public Task<bool> OpenExport(string runStepXml, string schemaXml, string configParametersXml, string extensionsDirectory)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "OpenExport requires the consumer host, which was not initialised.");
                }

                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);

                OpenExportConnectionRunStep runStep =
                    MmsPipeSerializer.DeserializeXml<OpenExportConnectionRunStep>(runStepXml);

                Schema schema = string.IsNullOrEmpty(schemaXml)
                    ? Schema.Create()
                    : MmsPipeSerializer.DeserializeXml<Schema>(schemaXml);

                InjectExtensionsDirectory(extensionsDirectory);

                ExportContext context = ExportContext.Create(runStep, schema);

                _exportOrchestrator = new Ecma2ExportOrchestrator(_workerHost.Services);

                await _exportOrchestrator.OpenAsync(context);

                return true;
            });
        }

        /// <summary>
        /// Handles the <c>PutExport</c> JSON-RPC request.
        /// </summary>
        /// <param name="entriesXml">The batch of entries (serialised <see cref="List{T}"/> of <see cref="CSEntryChange"/>).</param>
        /// <returns>The per-entry results (serialised <see cref="List{T}"/> of <see cref="CSEntryChangeResult"/>).</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown (and surfaced as a JSON-RPC error) when this method is called before
        /// <see cref="OpenExport"/> has established an export session.
        /// </exception>
        public Task<string> PutExport(string entriesXml)
        {
            return GuardAsync(async () =>
            {
                if (_exportOrchestrator == null)
                {
                    throw new InvalidOperationException(
                        "PutExport was called before OpenExport. Call OpenExport first to establish an export session.");
                }

                List<CSEntryChange> entries = MmsPipeSerializer.DeserializeXml<List<CSEntryChange>>(entriesXml);

                IList<CSEntryChangeResult> results = await _exportOrchestrator.PutAsync(
                    entries,
                    CancellationToken.None);

                List<CSEntryChangeResult> resultList = new List<CSEntryChangeResult>(results);
                return MmsPipeSerializer.SerializeXml<List<CSEntryChangeResult>>(resultList);
            });
        }

        /// <summary>
        /// Handles the <c>CloseExport</c> JSON-RPC request.
        /// Finalises the export orchestrator and discards it.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public Task<bool> CloseExport()
        {
            return GuardAsync(async () =>
            {
                if (_exportOrchestrator != null)
                {
                    await _exportOrchestrator.CloseAsync();
                    _exportOrchestrator = null;
                }

                return true;
            });
        }

        // -------------------------------------------------------------------------
        // Password session handlers
        //
        // The identity object crosses as a CSEntryIdentity MmsPipeSerializer XML string; secrets cross as
        // plain string arguments over the same-identity local pipe. Secrets are wrapped in a SecureString
        // immediately, disposed in a finally block, and NEVER logged or placed in an exception message.
        // -------------------------------------------------------------------------

        /// <summary>
        /// Handles the <c>OpenPassword</c> JSON-RPC request. Creates a <see cref="PasswordContext"/>,
        /// constructs an <see cref="Ecma2PasswordOrchestrator"/> backed by the consumer's DI container,
        /// and initialises all password providers.
        /// </summary>
        /// <param name="configParametersXml">
        /// The current configuration parameters as a serialised <see cref="List{T}"/> of real
        /// <see cref="ConfigParameter"/>, or null/empty when none.
        /// </param>
        /// <param name="partitionXml">The real <see cref="Partition"/> as XML, or null/empty when none.</param>
        /// <param name="extensionsDirectory">
        /// The host's <c>Utils.ExtensionsDirectory</c>, captured engine-side by the shim and injected here so
        /// providers can read it via <see cref="IEngineServices"/>. May be null.
        /// </param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the consumer host was not initialised.
        /// </exception>
        public Task<bool> OpenPassword(string configParametersXml, string partitionXml, string extensionsDirectory)
        {
            return GuardAsync(async () =>
            {
                if (_workerHost == null)
                {
                    throw new InvalidOperationException(
                        "OpenPassword requires the consumer host, which was not initialised.");
                }

                KeyedCollection<string, ConfigParameter> configParams = BuildConfigParameterCollection(configParametersXml);
                _workerHost.BuildContainer(configParams);

                InjectExtensionsDirectory(extensionsDirectory);

                Partition partition = string.IsNullOrEmpty(partitionXml)
                    ? null
                    : MmsPipeSerializer.DeserializeXml<Partition>(partitionXml);

                PasswordContext context = PasswordContext.Create(partition);

                _passwordOrchestrator = new Ecma2PasswordOrchestrator(_workerHost.Services);

                await _passwordOrchestrator.OpenAsync(context);

                return true;
            });
        }

        /// <summary>
        /// Handles the <c>GetSecurityLevel</c> JSON-RPC request. The framework always reports secure
        /// transport for password operations.
        /// </summary>
        /// <returns>The <see cref="ConnectionSecurityLevel"/> name (always <c>Secure</c>).</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown (and surfaced as a JSON-RPC error) when this method is called before
        /// <see cref="OpenPassword"/> has established a password session.
        /// </exception>
        public string GetSecurityLevel()
        {
            return Guard(() =>
            {
                if (_passwordOrchestrator == null)
                {
                    throw new InvalidOperationException(
                        "GetSecurityLevel was called before OpenPassword. Call OpenPassword first to establish a password session.");
                }

                return ConnectionSecurityLevel.Secure.ToString();
            });
        }

        /// <summary>
        /// Handles the <c>SetPassword</c> JSON-RPC request. Deserialises the <see cref="CSEntryIdentity"/>,
        /// wraps the secret in a <see cref="SecureString"/>, delegates to the
        /// <see cref="Ecma2PasswordOrchestrator"/>, and disposes the SecureString in a <c>finally</c> block.
        /// </summary>
        /// <remarks>
        /// SECURITY: <paramref name="newPassword"/> is wrapped in a <see cref="SecureString"/> immediately
        /// and disposed in a <c>finally</c> block. No password value is logged or placed in any exception
        /// message; a provider exception propagates as a JSON-RPC error with the provider-supplied (secret-
        /// free) message only.
        /// </remarks>
        /// <param name="identityXml">The <see cref="CSEntryIdentity"/> as a <see cref="MmsPipeSerializer"/> XML string.</param>
        /// <param name="newPassword">The new password plaintext.</param>
        /// <param name="options">The <see cref="PasswordOptions"/> name.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown (and surfaced as a JSON-RPC error) when this method is called before
        /// <see cref="OpenPassword"/> has established a password session.
        /// </exception>
        public Task<bool> SetPassword(string identityXml, string newPassword, string options)
        {
            return GuardAsync(async () =>
            {
                if (_passwordOrchestrator == null)
                {
                    throw new InvalidOperationException(
                        "SetPassword was called before OpenPassword. Call OpenPassword first to establish a password session.");
                }

                CSEntryIdentity identity = MmsPipeSerializer.DeserializeXml<CSEntryIdentity>(identityXml);
                CSEntry entry = PasswordIdentity.DetachedCSEntryFactory.Create(identity);
                PasswordOptions passwordOptions = (PasswordOptions)Enum.Parse(typeof(PasswordOptions), options, true);
                SecureString newPasswordSecure = BuildSecureString(newPassword);

                try
                {
                    await _passwordOrchestrator.SetPasswordAsync(entry, newPasswordSecure, passwordOptions);
                }
                finally
                {
                    newPasswordSecure.Dispose();
                }

                return true;
            });
        }

        /// <summary>
        /// Handles the <c>ChangePassword</c> JSON-RPC request. Deserialises the <see cref="CSEntryIdentity"/>,
        /// wraps both secrets in <see cref="SecureString"/> instances, delegates to the
        /// <see cref="Ecma2PasswordOrchestrator"/>, and disposes both SecureStrings in a <c>finally</c> block.
        /// </summary>
        /// <remarks>
        /// SECURITY: both plaintext values are wrapped in <see cref="SecureString"/> instances immediately
        /// and disposed in a <c>finally</c> block. No password value is logged or placed in any exception
        /// message; a provider exception propagates as a JSON-RPC error with the provider-supplied (secret-
        /// free) message only.
        /// </remarks>
        /// <param name="identityXml">The <see cref="CSEntryIdentity"/> as a <see cref="MmsPipeSerializer"/> XML string.</param>
        /// <param name="oldPassword">The current password plaintext.</param>
        /// <param name="newPassword">The new password plaintext.</param>
        /// <returns><c>true</c> on success.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown (and surfaced as a JSON-RPC error) when this method is called before
        /// <see cref="OpenPassword"/> has established a password session.
        /// </exception>
        public Task<bool> ChangePassword(string identityXml, string oldPassword, string newPassword)
        {
            return GuardAsync(async () =>
            {
                if (_passwordOrchestrator == null)
                {
                    throw new InvalidOperationException(
                        "ChangePassword was called before OpenPassword. Call OpenPassword first to establish a password session.");
                }

                CSEntryIdentity identity = MmsPipeSerializer.DeserializeXml<CSEntryIdentity>(identityXml);
                CSEntry entry = PasswordIdentity.DetachedCSEntryFactory.Create(identity);
                SecureString oldPasswordSecure = BuildSecureString(oldPassword);
                SecureString newPasswordSecure = BuildSecureString(newPassword);

                try
                {
                    await _passwordOrchestrator.ChangePasswordAsync(entry, oldPasswordSecure, newPasswordSecure);
                }
                finally
                {
                    oldPasswordSecure.Dispose();
                    newPasswordSecure.Dispose();
                }

                return true;
            });
        }

        /// <summary>
        /// Handles the <c>ClosePassword</c> JSON-RPC request. Finalises the password orchestrator and
        /// discards it. Idempotent.
        /// </summary>
        /// <returns><c>true</c> on success.</returns>
        public Task<bool> ClosePassword()
        {
            return GuardAsync(async () =>
            {
                if (_passwordOrchestrator != null)
                {
                    await _passwordOrchestrator.CloseAsync();
                    _passwordOrchestrator = null;
                }

                return true;
            });
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Deserialises a serialised <see cref="List{T}"/> of real <see cref="ConfigParameter"/> into the
        /// keyed collection the DI host expects. A null/empty payload yields an empty collection.
        /// </summary>
        private static KeyedCollection<string, ConfigParameter> BuildConfigParameterCollection(string configParametersXml)
        {
            EmptyConfigParameterCollection collection = new EmptyConfigParameterCollection();

            if (string.IsNullOrEmpty(configParametersXml))
            {
                return collection;
            }

            List<ConfigParameter> parameters =
                MmsPipeSerializer.DeserializeXml<List<ConfigParameter>>(configParametersXml);

            if (parameters != null)
            {
                foreach (ConfigParameter parameter in parameters)
                {
                    collection.Add(parameter);
                }
            }

            return collection;
        }

        /// <summary>
        /// Stores the host's injected <c>Utils.ExtensionsDirectory</c> on the worker's
        /// <see cref="Internal.EngineServices"/> singleton so providers can read it via
        /// <see cref="IEngineServices"/>. A null or empty value is left unset (the accessor returns null),
        /// since the value is genuinely optional — only ACMA and the SSH MA read it, and a consumer that does
        /// not is unaffected.
        /// </summary>
        private void InjectExtensionsDirectory(string extensionsDirectory)
        {
            if (string.IsNullOrEmpty(extensionsDirectory))
            {
                return;
            }

            Internal.EngineServices engineServices = _workerHost.Services.GetService<Internal.EngineServices>();

            if (engineServices != null)
            {
                engineServices.ExtensionsDirectory = extensionsDirectory;
            }
        }

        /// <summary>
        /// Wraps a plaintext secret in a read-only <see cref="SecureString"/>.
        /// </summary>
        /// <remarks>
        /// SECURITY: the resulting SecureString is the caller's responsibility to dispose (the password
        /// handlers do so in a <c>finally</c> block). The plaintext argument is not logged or retained here.
        /// </remarks>
        private static SecureString BuildSecureString(string plain)
        {
            SecureString secure = new SecureString();

            if (plain != null)
            {
                foreach (char c in plain)
                {
                    secure.AppendChar(c);
                }
            }

            secure.MakeReadOnly();
            return secure;
        }
    }
}
