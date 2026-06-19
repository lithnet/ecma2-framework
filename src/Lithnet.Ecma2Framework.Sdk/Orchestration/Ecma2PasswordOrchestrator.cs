using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// Orchestrates a provider-based password operation.  One orchestrator instance is created
    /// per password session and is driven by the worker host via <see cref="OpenAsync"/>,
    /// one or more <see cref="SetPasswordAsync"/> / <see cref="ChangePasswordAsync"/> calls,
    /// and a final <see cref="CloseAsync"/>.
    /// </summary>
    /// <remarks>
    /// Security:
    /// <list type="bullet">
    ///   <item>No password value is ever logged, written to any output stream, or placed in an
    ///     exception message.  The <see cref="SecureString"/> parameters are passed directly to
    ///     the provider and are disposed by the caller; this class never retains them.</item>
    ///   <item>Exception messages propagated from providers must be secret-free by construction.</item>
    /// </list>
    ///
    /// Error handling:
    /// <list type="bullet">
    ///   <item><see cref="OpenAsync"/> throws <see cref="InvalidOperationException"/> when no
    ///     <see cref="IObjectPasswordProvider"/> is registered, and propagates any non-
    ///     <see cref="NotImplementedException"/> thrown during provider initialisation.</item>
    ///   <item><see cref="SetPasswordAsync"/> and <see cref="ChangePasswordAsync"/> throw
    ///     <see cref="InvalidOperationException"/> when no provider claims the entry (i.e.
    ///     <see cref="IObjectPasswordProvider.CanPerformPasswordOperationAsync"/> returns false
    ///     for all registered providers).</item>
    ///   <item><see cref="CloseAsync"/> does not throw.</item>
    /// </list>
    /// </remarks>
    internal sealed class Ecma2PasswordOrchestrator
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogger<Ecma2PasswordOrchestrator> logger;

        // Lazily-resolved and cached list of password providers for the lifetime of this session.
        private List<IObjectPasswordProvider> providerCache;

        /// <summary>
        /// Initialises a new <see cref="Ecma2PasswordOrchestrator"/> using the supplied service provider.
        /// All <see cref="IObjectPasswordProvider"/> instances are resolved from the container when
        /// <see cref="OpenAsync"/> is called.
        /// </summary>
        /// <param name="serviceProvider">The DI container built for this password session.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceProvider"/> is null.</exception>
        public Ecma2PasswordOrchestrator(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new ArgumentNullException("serviceProvider");
            }

            this.serviceProvider = serviceProvider;
            this.logger = serviceProvider.GetService<ILogger<Ecma2PasswordOrchestrator>>();
        }

        /// <summary>
        /// Resolves all registered <see cref="IObjectPasswordProvider"/> instances and calls
        /// <see cref="IObjectPasswordProvider.InitializeAsync"/> on each one.
        /// </summary>
        /// <param name="context">The password context for this session.</param>
        /// <returns>A task that completes when all providers have been initialised.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no password providers are registered.</exception>
        public async Task OpenAsync(PasswordContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            this.providerCache = new List<IObjectPasswordProvider>(
                this.serviceProvider.GetServices<IObjectPasswordProvider>());

            if (this.providerCache.Count == 0)
            {
                throw new InvalidOperationException(
                    "No IObjectPasswordProvider implementations are registered. " +
                    "Register at least one password provider in the consumer startup.");
            }

            this.LogInformation("Starting password session ({0} provider(s) registered)", this.providerCache.Count);

            foreach (IObjectPasswordProvider provider in this.providerCache)
            {
                try
                {
                    await provider.InitializeAsync(context);
                }
                catch (NotImplementedException) { }
            }

            this.LogInformation("Password providers initialised");
        }

        /// <summary>
        /// Routes a set-password operation to the first <see cref="IObjectPasswordProvider"/>
        /// whose <see cref="IObjectPasswordProvider.CanPerformPasswordOperationAsync"/> returns
        /// true for the given entry.
        /// </summary>
        /// <remarks>
        /// SECURITY: The <paramref name="newPassword"/> <see cref="SecureString"/> is passed
        /// directly to the provider.  It is never logged, stored, or converted to plaintext by
        /// this method.  The caller is responsible for disposing the SecureString.
        /// </remarks>
        /// <param name="entry">The connector space entry whose password is being set.</param>
        /// <param name="newPassword">The new password. Must not be null.</param>
        /// <param name="options">Flags that modify set-password behaviour.</param>
        /// <returns>A task that completes when the provider has processed the operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entry"/> or <paramref name="newPassword"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called before <see cref="OpenAsync"/>, or when no provider claims the entry.
        /// </exception>
        public async Task SetPasswordAsync(CSEntry entry, SecureString newPassword, PasswordOptions options)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }

            if (newPassword == null)
            {
                throw new ArgumentNullException("newPassword");
            }

            if (this.providerCache == null)
            {
                throw new InvalidOperationException("SetPasswordAsync was called before OpenAsync.");
            }

            // SECURITY: DN is logged here. No password value is logged anywhere in this method.
            this.LogInformation("Setting password for: {0}", entry.DN);

            IObjectPasswordProvider provider = await this.FindProviderForEntryAsync(entry);
            await provider.SetPasswordAsync(entry, newPassword, options);

            this.LogInformation("Successfully set password for: {0}", entry.DN);
        }

        /// <summary>
        /// Routes a change-password operation to the first <see cref="IObjectPasswordProvider"/>
        /// whose <see cref="IObjectPasswordProvider.CanPerformPasswordOperationAsync"/> returns
        /// true for the given entry.
        /// </summary>
        /// <remarks>
        /// SECURITY: The <paramref name="oldPassword"/> and <paramref name="newPassword"/>
        /// <see cref="SecureString"/> values are passed directly to the provider.  They are
        /// never logged, stored, or converted to plaintext by this method.  The caller is
        /// responsible for disposing both SecureStrings.
        /// </remarks>
        /// <param name="entry">The connector space entry whose password is being changed.</param>
        /// <param name="oldPassword">The current password. Must not be null.</param>
        /// <param name="newPassword">The new password. Must not be null.</param>
        /// <returns>A task that completes when the provider has processed the operation.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="entry"/>, <paramref name="oldPassword"/>, or
        /// <paramref name="newPassword"/> is null.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when called before <see cref="OpenAsync"/>, or when no provider claims the entry.
        /// </exception>
        public async Task ChangePasswordAsync(CSEntry entry, SecureString oldPassword, SecureString newPassword)
        {
            if (entry == null)
            {
                throw new ArgumentNullException("entry");
            }

            if (oldPassword == null)
            {
                throw new ArgumentNullException("oldPassword");
            }

            if (newPassword == null)
            {
                throw new ArgumentNullException("newPassword");
            }

            if (this.providerCache == null)
            {
                throw new InvalidOperationException("ChangePasswordAsync was called before OpenAsync.");
            }

            // SECURITY: DN is logged here. No password value is logged anywhere in this method.
            this.LogInformation("Changing password for: {0}", entry.DN);

            IObjectPasswordProvider provider = await this.FindProviderForEntryAsync(entry);
            await provider.ChangePasswordAsync(entry, oldPassword, newPassword);

            this.LogInformation("Successfully changed password for: {0}", entry.DN);
        }

        /// <summary>
        /// Closes the password session.  Idempotent — safe to call even when
        /// <see cref="OpenAsync"/> was not called.
        /// </summary>
        /// <returns>A completed task.</returns>
        public Task CloseAsync()
        {
            this.LogInformation("Password session closed");
            return Task.CompletedTask;
        }

        // -------------------------------------------------------------------------
        // Private helpers
        // -------------------------------------------------------------------------

        private async Task<IObjectPasswordProvider> FindProviderForEntryAsync(CSEntry entry)
        {
            foreach (IObjectPasswordProvider provider in this.providerCache)
            {
                try
                {
                    if (await provider.CanPerformPasswordOperationAsync(entry))
                    {
                        return provider;
                    }
                }
                catch (NotImplementedException) { }
            }

            throw new InvalidOperationException(
                string.Format(
                    "A password provider for the object type '{0}' could not be found.",
                    entry.ObjectType));
        }

        // -------------------------------------------------------------------------
        // Logging helpers (guard against null logger)
        // -------------------------------------------------------------------------

        private void LogInformation(string message, params object[] args)
        {
            if (this.logger != null)
            {
                this.logger.LogInformation(message, args);
            }
        }
    }
}
