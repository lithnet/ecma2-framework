using System.Collections.Generic;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework
{
    /// <summary>
    /// A single present attribute carried on a <see cref="CSEntryIdentity"/>: a name, the host
    /// <see cref="AttributeType"/>, a multi-valued flag, and the present value(s).
    /// </summary>
    /// <remarks>
    /// This is a plain framework data object, NOT a host <c>Attrib</c>. It carries only the
    /// identity-relevant subset a password provider needs to locate a target account (in particular
    /// the anchor attribute value(s) — the GAP-7 fix for directories that key on an anchor rather than
    /// the DN). The provider only READS it; it never travels back to the host engine.
    ///
    /// Values are the boxed CLR forms the host exposes for the attribute's <see cref="AttributeType"/>:
    /// <see cref="string"/> for String, <see cref="long"/> for Integer, <see cref="bool"/> for Boolean,
    /// <see cref="T:System.Byte[]"/> for Binary, and the string form of the reference for Reference.
    /// </remarks>
#if ECMA2_SHIM_INTERNAL
    internal sealed class CSEntryIdentityAttribute
#else
    public sealed class CSEntryIdentityAttribute
#endif
    {
        /// <summary>
        /// Initialises a new <see cref="CSEntryIdentityAttribute"/>.
        /// </summary>
        /// <param name="name">The attribute name.</param>
        /// <param name="dataType">The host attribute data type.</param>
        /// <param name="isMultivalued">Whether the attribute is multi-valued.</param>
        /// <param name="values">The present value(s). A null list is normalised to an empty list.</param>
        public CSEntryIdentityAttribute(string name, AttributeType dataType, bool isMultivalued, IList<object> values)
        {
            this.Name = name;
            this.DataType = dataType;
            this.IsMultivalued = isMultivalued;
            this.Values = values ?? new List<object>();
        }

        /// <summary>Gets the attribute name.</summary>
        public string Name { get; }

        /// <summary>Gets the host attribute data type.</summary>
        public AttributeType DataType { get; }

        /// <summary>Gets a value indicating whether the attribute is multi-valued.</summary>
        public bool IsMultivalued { get; }

        /// <summary>Gets the present value(s) for this attribute as boxed CLR values.</summary>
        public IList<object> Values { get; }

        /// <summary>
        /// Gets the first value for this attribute, or null when no value is present.
        /// </summary>
        public object Value
        {
            get
            {
                if (this.Values.Count == 0)
                {
                    return null;
                }

                return this.Values[0];
            }
        }
    }
}
