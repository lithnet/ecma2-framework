using System;
using System.Reflection;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Re-throws the EXACT real host exception from an MmsExceptionEnvelope on the shim side (mapping Section D),
    // so FIM's type-driven handling fires (WarningNoWatermarkException is a warning; OldPasswordIncorrectException
    // vs PasswordPolicyViolationException stay distinct; the 5 DN types expose the engine-recomputed DN getter
    // from the carried MAName/DN strings; etc.).
    //
    // Rebuild strategy (uniform across all 42, identical on net48 and net8): construct the exact host type via
    // its parameterless ctor, then restore the carried base Message + InnerException via the BCL Exception
    // backing fields (_message / _innerException) and set the type-specific declared backing fields (m_*) by
    // reflection. The carried Message is restored verbatim (never re-derived from a ctor format string), and the
    // declared fields drive the diagnostic getters (including the engine-recomputed DN : ReferenceValue getter,
    // which resolves on-engine from the carried m_MAName / m_DN strings).
    //
    // A non-host carrier (IsHostException = false) surfaces as a host ExtensibleExtensionException whose message
    // preserves the concrete worker type name + original message, with the full inner chain rebuilt, so nothing
    // is dropped on the host side.
#if ECMA2_SHIM_INTERNAL
    internal static class MmsExceptionReconstructor
#else
    public static class MmsExceptionReconstructor
#endif
    {
        private const BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        private static readonly FieldInfo ExceptionMessageField =
            typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly FieldInfo ExceptionInnerField =
            typeof(Exception).GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Reconstructs the exception described by <paramref name="envelope"/> without throwing it.
        /// Returns null when <paramref name="envelope"/> is null.
        /// </summary>
        public static Exception Reconstruct(MmsExceptionEnvelope envelope)
        {
            if (envelope == null)
            {
                return null;
            }

            Exception inner = Reconstruct(envelope.InnerException);

            if (envelope.IsHostException)
            {
                Type hostType = MmsExceptionTypeRegistry.Resolve(envelope.TypeName);

                if (hostType != null)
                {
                    return BuildHostException(envelope, hostType, inner);
                }

                // The envelope claims a host type we cannot resolve (a host-revision skew). Do not silently
                // drop the detail: fall through to the carrier so the type name + message + inner survive.
            }

            return BuildCarrier(envelope, inner);
        }

        /// <summary>
        /// Reconstructs and throws the exception described by <paramref name="envelope"/>. Throws an
        /// <see cref="InvalidOperationException"/> when the envelope is null (a null error envelope is itself a
        /// transport defect, never a success).
        /// </summary>
        public static void Throw(MmsExceptionEnvelope envelope)
        {
            Exception exception = Reconstruct(envelope);

            if (exception == null)
            {
                throw new InvalidOperationException(
                    "The worker reported a failure but supplied no exception detail.");
            }

            throw exception;
        }

        private static Exception BuildHostException(MmsExceptionEnvelope envelope, Type hostType, Exception inner)
        {
            Exception exception = (Exception)Activator.CreateInstance(hostType, true);

            RestoreBase(exception, envelope.Message, inner);

            foreach (string fieldName in MmsExceptionTypeRegistry.DeclaredFields(hostType.Name))
            {
                object value = SelectFieldValue(envelope, fieldName);
                SetField(exception, hostType, fieldName, value);
            }

            return exception;
        }

        private static object SelectFieldValue(MmsExceptionEnvelope envelope, string fieldName)
        {
            switch (fieldName)
            {
                case MmsExceptionTypeRegistry.FieldAttributeName:
                    return envelope.AttributeName;

                case MmsExceptionTypeRegistry.FieldMAName:
                    return envelope.MAName;

                case MmsExceptionTypeRegistry.FieldDN:
                    return envelope.Dn;

                case MmsExceptionTypeRegistry.FieldObjectClasses:
                    return envelope.ObjectClasses;

                case MmsExceptionTypeRegistry.FieldPrimaryObjectClass:
                    return envelope.ObjectType;

                case MmsExceptionTypeRegistry.FieldClassName:
                    return envelope.ClassName;

                case MmsExceptionTypeRegistry.FieldParameterName:
                    return envelope.ParameterName;

                default:
                    return null;
            }
        }

        private static Exception BuildCarrier(MmsExceptionEnvelope envelope, Exception inner)
        {
            // Surface the non-host worker exception as a host ExtensibleExtensionException, preserving the
            // concrete worker type name + message in the carrier's message and the full inner chain.
            string typeName = string.IsNullOrEmpty(envelope.TypeName) ? "(unknown type)" : envelope.TypeName;
            string original = envelope.Message ?? string.Empty;
            string carrierMessage = $"Worker exception [{typeName}]: {original}";

            if (inner != null)
            {
                return new ExtensibleExtensionException(carrierMessage, inner);
            }

            return new ExtensibleExtensionException(carrierMessage);
        }

        private static void RestoreBase(Exception exception, string message, Exception inner)
        {
            if (ExceptionMessageField != null)
            {
                ExceptionMessageField.SetValue(exception, message);
            }

            if (inner != null && ExceptionInnerField != null)
            {
                ExceptionInnerField.SetValue(exception, inner);
            }
        }

        private static void SetField(Exception exception, Type type, string fieldName, object value)
        {
            Type current = type;

            while (current != null)
            {
                FieldInfo field = current.GetField(fieldName, InstanceFieldFlags);

                if (field != null)
                {
                    field.SetValue(exception, value);
                    return;
                }

                current = current.BaseType;
            }
        }
    }
}
