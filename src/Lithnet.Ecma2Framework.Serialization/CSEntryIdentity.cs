using System;
using System.Collections.Generic;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// A clearly-distinct, framework-owned identity object handed to <see cref="IObjectPasswordProvider"/>
    /// in place of the host <c>Microsoft.MetadirectoryServices.CSEntry</c>.
    /// </summary>
    /// <remarks>
    /// The host <c>CSEntry</c> is an abstract live-engine object with no constructible form, so it cannot
    /// cross the worker/shim pipe. Rather than fake a <c>CSEntry</c>, the password path carries this plain
    /// data object: it holds the identity subset a password provider needs to locate the target account.
    /// It is NOT a <c>CSEntry</c> subclass and does not pretend to be one.
    ///
    /// The shim extracts the fields from the live <c>CSEntry</c> (DN, RDN, ObjectType, ObjectClass, MA name,
    /// and every present attribute with its value(s)) on the net48 side, serialises this object across the
    /// pipe, and the worker hands the reconstructed object to the provider. The provider only READS it;
    /// nothing on the password path ever travels back to the host engine.
    ///
    /// Present attributes (including anchor attributes WITH their values) are carried so that directories
    /// keying on an anchor rather than the DN can still find the account (the GAP-7 fix). Use
    /// <see cref="GetAttribute"/>, <see cref="GetValue"/>, or the <see cref="this[string]"/> indexer to read
    /// an attribute by name.
    /// </remarks>
#if ECMA2_SHIM_INTERNAL
    internal sealed class CSEntryIdentity
#else
    public sealed class CSEntryIdentity
#endif
    {
        private readonly Dictionary<string, CSEntryIdentityAttribute> attributesByName =
            new Dictionary<string, CSEntryIdentityAttribute>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initialises a new, empty <see cref="CSEntryIdentity"/>. Populate the identity fields and add
        /// attributes via <see cref="AddAttribute"/>.
        /// </summary>
        public CSEntryIdentity()
        {
            this.ObjectClass = new List<string>();
            this.Attributes = new List<CSEntryIdentityAttribute>();
        }

        /// <summary>Gets or sets the distinguished name of the entry.</summary>
        public string DN { get; set; }

        /// <summary>Gets or sets the relative distinguished name of the entry.</summary>
        public string RDN { get; set; }

        /// <summary>Gets or sets the primary object type of the entry.</summary>
        public string ObjectType { get; set; }

        /// <summary>Gets the object class list of the entry.</summary>
        public IList<string> ObjectClass { get; }

        /// <summary>Gets or sets the name of the management agent the entry belongs to.</summary>
        public string MAName { get; set; }

        /// <summary>Gets the present attributes of the entry.</summary>
        public IList<CSEntryIdentityAttribute> Attributes { get; }

        /// <summary>
        /// Gets the attribute with the given name, or null when the entry has no such present attribute.
        /// Name matching is case-insensitive.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        public CSEntryIdentityAttribute this[string name]
        {
            get
            {
                return this.GetAttribute(name);
            }
        }

        /// <summary>
        /// Adds a present attribute to the entry. A later add for the same name (case-insensitive) replaces
        /// the earlier one in the by-name lookup; the attribute is appended to <see cref="Attributes"/>.
        /// </summary>
        /// <param name="attribute">The attribute to add. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="attribute"/> is null.</exception>
        public void AddAttribute(CSEntryIdentityAttribute attribute)
        {
            if (attribute == null)
            {
                throw new ArgumentNullException(nameof(attribute));
            }

            this.Attributes.Add(attribute);

            if (attribute.Name != null)
            {
                this.attributesByName[attribute.Name] = attribute;
            }
        }

        /// <summary>
        /// Gets the attribute with the given name, or null when the entry has no such present attribute.
        /// Name matching is case-insensitive.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        public CSEntryIdentityAttribute GetAttribute(string name)
        {
            if (name == null)
            {
                return null;
            }

            CSEntryIdentityAttribute attribute;
            return this.attributesByName.TryGetValue(name, out attribute) ? attribute : null;
        }

        /// <summary>
        /// Gets the first value of the named attribute, or null when the entry has no such present attribute
        /// (or the attribute carries no value). Name matching is case-insensitive.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        public object GetValue(string name)
        {
            CSEntryIdentityAttribute attribute = this.GetAttribute(name);
            return attribute == null ? null : attribute.Value;
        }

        /// <summary>
        /// Gets all values of the named attribute, or an empty list when the entry has no such present
        /// attribute. Name matching is case-insensitive.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        public IList<object> GetValues(string name)
        {
            CSEntryIdentityAttribute attribute = this.GetAttribute(name);
            return attribute == null ? new List<object>() : attribute.Values;
        }
    }
}
