using System;
using System.Collections.Generic;
using Lithnet.Ecma2Framework;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Shim
{
    /// <summary>
    /// Extracts a framework-owned <see cref="CSEntryIdentity"/> from the live host <see cref="CSEntry"/>
    /// the MIM engine hands to <c>IMAExtensible2Password</c> on the net48 side.
    /// </summary>
    /// <remarks>
    /// The host <see cref="CSEntry"/> is an abstract live-engine object that cannot cross the worker pipe,
    /// so the shim copies the identity subset a password provider needs into a plain
    /// <see cref="CSEntryIdentity"/>: DN, RDN, ObjectType, ObjectClass, MA name, and every present attribute
    /// with its value(s). Carrying the present attribute values (in particular the anchor attribute value(s))
    /// is the GAP-7 fix: directories that locate an account by an anchor rather than the DN can still find it.
    ///
    /// <para>The values are read via the host <see cref="Attrib"/> accessors, selecting the typed getter that
    /// matches the attribute's <see cref="AttributeType"/>. No value is logged. The identity object travels
    /// one direction only (shim to worker to provider); it never returns to the host engine.</para>
    /// </remarks>
    internal static class RealCSEntryToIdentity
    {
        /// <summary>
        /// Builds a <see cref="CSEntryIdentity"/> from a live host <see cref="CSEntry"/>.
        /// </summary>
        /// <param name="csentry">The live connector-space entry. Must not be null.</param>
        /// <returns>The extracted identity object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="csentry"/> is null.</exception>
        public static CSEntryIdentity Read(CSEntry csentry)
        {
            if (csentry == null)
            {
                throw new ArgumentNullException("csentry");
            }

            CSEntryIdentity identity = new CSEntryIdentity
            {
                // DN is a ReferenceValue; null for disconnected/projected entries.
                DN = csentry.DN == null ? null : csentry.DN.ToString(),
                RDN = csentry.RDN,
                ObjectType = csentry.ObjectType,
                // MA may be null in some call contexts; carry the name when present.
                MAName = csentry.MA == null ? null : csentry.MA.Name,
            };

            // ObjectClass may be null; guard before enumerating.
            if (csentry.ObjectClass != null)
            {
                foreach (Value value in csentry.ObjectClass)
                {
                    identity.ObjectClass.Add(value.ToString());
                }
            }

            // Enumerate the present attribute names and copy each present attribute with its value(s).
            // The CSEntry enumerator yields attribute names; the indexer returns the Attrib.
            foreach (string attributeName in csentry)
            {
                Attrib attrib = csentry[attributeName];

                if (attrib == null || !attrib.IsPresent)
                {
                    continue;
                }

                identity.AddAttribute(ReadAttribute(attrib));
            }

            return identity;
        }

        /// <summary>
        /// Reads a single present host <see cref="Attrib"/> into a <see cref="CSEntryIdentityAttribute"/>,
        /// selecting the typed value accessor that matches the attribute's <see cref="AttributeType"/>.
        /// </summary>
        private static CSEntryIdentityAttribute ReadAttribute(Attrib attrib)
        {
            List<object> values = new List<object>();

            if (attrib.IsMultivalued)
            {
                ReadMultiValuedValues(attrib, values);
            }
            else
            {
                object value = ReadSingleValuedValue(attrib);

                if (value != null)
                {
                    values.Add(value);
                }
            }

            return new CSEntryIdentityAttribute(attrib.Name, attrib.DataType, attrib.IsMultivalued, values);
        }

        private static object ReadSingleValuedValue(Attrib attrib)
        {
            switch (attrib.DataType)
            {
                case AttributeType.String:
                    return attrib.StringValue;

                case AttributeType.Integer:
                    return attrib.IntegerValue;

                case AttributeType.Boolean:
                    return attrib.BooleanValue;

                case AttributeType.Binary:
                    return attrib.BinaryValue;

                case AttributeType.Reference:
                    return attrib.ReferenceValue == null ? null : attrib.ReferenceValue.ToString();

                default:
                    // Undefined or unknown type: fall back to the string accessor.
                    return attrib.StringValue;
            }
        }

        private static void ReadMultiValuedValues(Attrib attrib, List<object> values)
        {
            ValueCollection collection = attrib.Values;

            if (collection == null)
            {
                return;
            }

            switch (attrib.DataType)
            {
                case AttributeType.Integer:
                    foreach (long integerValue in collection.ToIntegerArray())
                    {
                        values.Add(integerValue);
                    }

                    break;

                case AttributeType.Binary:
                    foreach (byte[] binaryValue in collection.ToByteArrays())
                    {
                        values.Add(binaryValue);
                    }

                    break;

                default:
                    // String, Reference, Boolean (multi-valued booleans are not expected, but the string
                    // form is a faithful representation), and Undefined: carry the string representation of
                    // each value. Reference values render as their DN string.
                    foreach (string stringValue in collection.ToStringArray())
                    {
                        values.Add(stringValue);
                    }

                    break;
            }
        }
    }
}
