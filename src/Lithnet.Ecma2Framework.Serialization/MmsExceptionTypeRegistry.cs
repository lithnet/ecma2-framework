using System;
using System.Collections.Generic;
using Microsoft.MetadirectoryServices;

namespace Lithnet.Ecma2Framework.Serialization
{
    // The single source of truth for the host exception hierarchy (mapping Section D): the 42 host exception
    // types keyed by simple name, and which declared Tree-A backing field(s) each carries. Both the worker-side
    // envelope factory and the shim-side reconstructor resolve types and fields through here, and the
    // completeness guard reflects the host assembly against this registry so a host-revision-added type or an
    // un-mapped declared field fails the build.
    //
    // Type identity is anchored to a real host type (typeof(MetadirectoryServicesException).Assembly) rather
    // than a hard-coded assembly string, so it resolves whichever copy of the MMS DLL the worker/shim loaded.
#if ECMA2_SHIM_INTERNAL
    internal static class MmsExceptionTypeRegistry
#else
    public static class MmsExceptionTypeRegistry
#endif
    {
        // Reflected backing-field names declared by the Tree-A types (mapping Section D). A type not listed
        // here declares no extra field (the Tree-A root and all 25 Tree-B types).
        internal const string FieldAttributeName = "m_attributeName";
        internal const string FieldMAName = "m_MAName";
        internal const string FieldDN = "m_DN";
        internal const string FieldObjectClasses = "m_ObjectClasses";
        internal const string FieldPrimaryObjectClass = "m_PrimaryObjectClass";
        internal const string FieldClassName = "m_className";
        internal const string FieldParameterName = "m_parameterName";

        // The 42 host exception type simple names -> their resolved host Type. Resolved once against the loaded
        // MMS assembly. A name that does not resolve throws at static-init (a broken host reference is a
        // fail-loud condition, never a silent miss).
        private static readonly Dictionary<string, Type> TypesByName = BuildTypeMap();

        // Per-type declared backing fields the envelope must carry, keyed by host type simple name. A type
        // absent from this map declares no extra field (only Message + InnerException cross).
        private static readonly Dictionary<string, string[]> DeclaredFieldsByType =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                // Tree-A single attribute-name field
                ["NoSuchAttributeException"] = new[] { FieldAttributeName },
                ["NoSuchAttributeInObjectTypeException"] = new[] { FieldAttributeName },
                ["AttributeDoesNotExistOnObjectException"] = new[] { FieldAttributeName },
                ["AttributeNotInInclusionListException"] = new[] { FieldAttributeName },
                ["AttributeNotPresentException"] = new[] { FieldAttributeName },
                ["AttributeNotDefinedAsSourceException"] = new[] { FieldAttributeName },
                ["TooManyValuesException"] = new[] { FieldAttributeName },

                // Tree-A class-name field (NoSuchObjectTypeException getter is ObjectType, field is m_className)
                ["NoSuchClassException"] = new[] { FieldClassName },
                ["NoSuchObjectTypeException"] = new[] { FieldClassName },

                // Tree-A parameter-name field
                ["NoSuchParameterException"] = new[] { FieldParameterName },

                // Tree-A MA-name only (no DN)
                ["NoSuchManagementAgentException"] = new[] { FieldMAName },

                // Tree-A MAName + DN (the engine recomputes the DN : ReferenceValue getter on-host)
                ["NoSuchObjectException"] = new[] { FieldMAName, FieldDN },
                ["InvalidDNException"] = new[] { FieldMAName, FieldDN },
                ["ObjectAlreadyExistsException"] = new[] { FieldMAName, FieldDN },
                ["MissingParentObjectException"] = new[] { FieldMAName, FieldDN },

                // Tree-A richest type: MAName + DN + ObjectClasses[] + PrimaryObjectClass (getter ObjectType)
                ["NoCompatiblePartitionFoundException"] =
                    new[] { FieldMAName, FieldDN, FieldObjectClasses, FieldPrimaryObjectClass },
            };

        /// <summary>
        /// Gets the host <see cref="Type"/> for the supplied simple name, or null when the name is not one of
        /// the 42 host exception types.
        /// </summary>
        public static Type Resolve(string typeName)
        {
            if (typeName == null)
            {
                return null;
            }

            TypesByName.TryGetValue(typeName, out Type type);
            return type;
        }

        /// <summary>
        /// Determines whether the supplied runtime type is one of the 42 host exception types (i.e. derives
        /// from <see cref="MetadirectoryServicesException"/> or <see cref="ExtensionException"/>).
        /// </summary>
        public static bool IsHostExceptionType(Type type)
        {
            if (type == null)
            {
                return false;
            }

            return typeof(MetadirectoryServicesException).IsAssignableFrom(type)
                   || typeof(ExtensionException).IsAssignableFrom(type);
        }

        /// <summary>
        /// Gets the declared backing-field names the envelope carries for the supplied host type simple name.
        /// Returns an empty array for a type that declares no extra field.
        /// </summary>
        public static string[] DeclaredFields(string typeName)
        {
            if (typeName != null && DeclaredFieldsByType.TryGetValue(typeName, out string[] fields))
            {
                return fields;
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// The simple names of all 42 host exception types. Used by the completeness guard.
        /// </summary>
        public static IEnumerable<string> AllHostExceptionTypeNames()
        {
            return TypesByName.Keys;
        }

        private static Dictionary<string, Type> BuildTypeMap()
        {
            string[] names =
            {
                // Tree A (17)
                "MetadirectoryServicesException",
                "NoSuchAttributeException",
                "NoSuchParameterException",
                "NoSuchAttributeInObjectTypeException",
                "AttributeDoesNotExistOnObjectException",
                "AttributeNotInInclusionListException",
                "AttributeNotPresentException",
                "AttributeNotDefinedAsSourceException",
                "TooManyValuesException",
                "NoSuchClassException",
                "NoSuchObjectTypeException",
                "NoSuchManagementAgentException",
                "NoSuchObjectException",
                "InvalidDNException",
                "ObjectAlreadyExistsException",
                "MissingParentObjectException",
                "NoCompatiblePartitionFoundException",

                // Tree B (25)
                "ExtensionException",
                "TerminateRunException",
                "DeclineMappingException",
                "UnexpectedDataException",
                "EntryPointNotImplementedException",
                "OldPasswordIncorrectException",
                "ObjectNotFoundException",
                "ObjectTypeNotSupportedException",
                "AccessDeniedException",
                "PasswordIllFormedException",
                "PasswordPolicyViolationException",
                "BadServerCredentialsException",
                "ServerDownException",
                "FailedConnectionException",
                "DroppedConnectionException",
                "WarningNoWatermarkException",
                "FailedSearchException",
                "PasswordExtensionException",
                "EndConnectionException",
                "ClosePasswordConnectionException",
                "EntryExportException",
                "FatalEntryExportException",
                "ExtensibleExtensionException",
                "FunctionEvaluationException",
                "ProvisioningBySyncRuleException",
            };

            const string ns = "Microsoft.MetadirectoryServices.";
            System.Reflection.Assembly hostAssembly = typeof(MetadirectoryServicesException).Assembly;

            Dictionary<string, Type> map = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (string name in names)
            {
                Type type = hostAssembly.GetType(ns + name, false);

                if (type == null)
                {
                    throw new InvalidOperationException(
                        $"Host exception type '{ns + name}' was not found in the loaded MMS assembly " +
                        $"'{hostAssembly.FullName}'. The exception-marshalling registry is out of step with the host.");
                }

                map[name] = type;
            }

            return map;
        }
    }
}
