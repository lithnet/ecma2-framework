using System;
using System.Reflection;

namespace Lithnet.Ecma2Framework.Serialization
{
    // Builds a faithful MmsExceptionEnvelope from an exception thrown inside the worker (mapping Section D).
    // A host MetadirectoryServicesException/ExtensionException subclass is serialized as itself (TypeName =
    // host simple name, IsHostException = true, declared Tree-A fields read by reflection); any other
    // exception is serialized as a non-host carrier (TypeName = concrete CLR full name, IsHostException =
    // false) so it is never dropped. The InnerException chain is walked recursively under the same scheme.
#if ECMA2_SHIM_INTERNAL
    internal static class MmsExceptionEnvelopeFactory
#else
    public static class MmsExceptionEnvelopeFactory
#endif
    {
        private const BindingFlags InstanceFieldFlags =
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>
        /// Builds an envelope for the supplied exception (and its inner chain). Returns null when
        /// <paramref name="exception"/> is null.
        /// </summary>
        public static MmsExceptionEnvelope FromException(Exception exception)
        {
            if (exception == null)
            {
                return null;
            }

            Type type = exception.GetType();
            bool isHost = MmsExceptionTypeRegistry.IsHostExceptionType(type);

            MmsExceptionEnvelope envelope = new MmsExceptionEnvelope
            {
                IsHostException = isHost,
                Message = exception.Message,
                InnerException = FromException(exception.InnerException),
            };

            if (isHost)
            {
                envelope.TypeName = type.Name;
                PopulateHostFields(envelope, exception, type);
            }
            else
            {
                // Non-host carrier: preserve the concrete type's full name so the shim's message can name it.
                envelope.TypeName = type.FullName;
            }

            return envelope;
        }

        private static void PopulateHostFields(MmsExceptionEnvelope envelope, Exception exception, Type type)
        {
            foreach (string fieldName in MmsExceptionTypeRegistry.DeclaredFields(type.Name))
            {
                object value = ReadField(exception, type, fieldName);

                switch (fieldName)
                {
                    case MmsExceptionTypeRegistry.FieldAttributeName:
                        envelope.AttributeName = (string)value;
                        break;

                    case MmsExceptionTypeRegistry.FieldMAName:
                        envelope.MAName = (string)value;
                        break;

                    case MmsExceptionTypeRegistry.FieldDN:
                        envelope.Dn = (string)value;
                        break;

                    case MmsExceptionTypeRegistry.FieldObjectClasses:
                        envelope.ObjectClasses = (string[])value;
                        break;

                    case MmsExceptionTypeRegistry.FieldPrimaryObjectClass:
                        envelope.ObjectType = (string)value;
                        break;

                    case MmsExceptionTypeRegistry.FieldClassName:
                        envelope.ClassName = (string)value;
                        break;

                    case MmsExceptionTypeRegistry.FieldParameterName:
                        envelope.ParameterName = (string)value;
                        break;
                }
            }
        }

        // Reads a declared backing field, walking the declared-type chain (the field is declared on the
        // concrete host type, but reflecting from the runtime type is robust to that).
        private static object ReadField(Exception exception, Type type, string fieldName)
        {
            Type current = type;

            while (current != null)
            {
                FieldInfo field = current.GetField(fieldName, InstanceFieldFlags | BindingFlags.DeclaredOnly);

                if (field != null)
                {
                    return field.GetValue(exception);
                }

                current = current.BaseType;
            }

            return null;
        }
    }
}
