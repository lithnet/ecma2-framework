using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Lithnet.Ecma2Framework.Shim.Build.Tests
{
    /// <summary>
    /// Reads the metadata facts Part C asserts on directly from a built shim assembly's PE image, without
    /// loading or executing it. Using <see cref="System.Reflection.Metadata.MetadataReader"/> (not
    /// Assembly.LoadFrom) is deliberate: the shim is a net48 assembly and this test host is net8, so loading
    /// it into the test process could fail to resolve its net48 dependencies. Pure metadata reads avoid that
    /// entirely - every fact here is decidable from the metadata tables alone.
    /// </summary>
    internal static class ShimMetadataReader
    {
        /// <summary>
        /// Reads the assembly name, the public exported types (with their declared interfaces), and the
        /// referenced assembly names from the assembly at <paramref name="assemblyPath"/>.
        /// </summary>
        public static ShimAssemblyMetadata Read(string assemblyPath)
        {
            using (FileStream stream = File.OpenRead(assemblyPath))
            using (PEReader peReader = new PEReader(stream))
            {
                MetadataReader reader = peReader.GetMetadataReader();

                string assemblyName = reader.GetString(reader.GetAssemblyDefinition().Name);

                List<ShimExportedType> publicTypes = ReadPublicExportedTypes(reader);
                List<string> referencedAssemblies = ReadReferencedAssemblyNames(reader);

                return new ShimAssemblyMetadata(assemblyName, publicTypes, referencedAssemblies);
            }
        }

        /// <summary>
        /// Enumerates the public (externally visible) type definitions and, for each, the full names of the
        /// interfaces it directly declares.
        /// </summary>
        private static List<ShimExportedType> ReadPublicExportedTypes(MetadataReader reader)
        {
            List<ShimExportedType> result = new List<ShimExportedType>();

            foreach (TypeDefinitionHandle handle in reader.TypeDefinitions)
            {
                TypeDefinition type = reader.GetTypeDefinition(handle);

                TypeAttributes visibility = type.Attributes & TypeAttributes.VisibilityMask;

                if (visibility != TypeAttributes.Public)
                {
                    // NestedPublic, NotPublic, and the various nested visibilities are not externally
                    // exported top-level public types; skip them. The shim has no public nested types.
                    continue;
                }

                string fullName = BuildFullName(reader, type.Namespace, type.Name);
                List<string> interfaces = ReadDeclaredInterfaceNames(reader, type);

                result.Add(new ShimExportedType(fullName, interfaces));
            }

            return result;
        }

        /// <summary>
        /// Resolves the full names of the interfaces a type directly declares, from its
        /// InterfaceImplementation rows. The interface handle may be a TypeReference (interface defined in
        /// another assembly, such as the host MMS) or a TypeDefinition (defined in this assembly).
        /// </summary>
        private static List<string> ReadDeclaredInterfaceNames(MetadataReader reader, TypeDefinition type)
        {
            List<string> interfaces = new List<string>();

            foreach (InterfaceImplementationHandle implHandle in type.GetInterfaceImplementations())
            {
                InterfaceImplementation impl = reader.GetInterfaceImplementation(implHandle);
                string name = ResolveTypeHandleName(reader, impl.Interface);

                if (name != null)
                {
                    interfaces.Add(name);
                }
            }

            return interfaces;
        }

        /// <summary>
        /// Resolves a type handle (TypeReference or TypeDefinition) to its namespace-qualified full name.
        /// Returns null for handle kinds the shim does not use for interfaces (for example TypeSpecification).
        /// </summary>
        private static string ResolveTypeHandleName(MetadataReader reader, EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeReference)
            {
                TypeReference typeRef = reader.GetTypeReference((TypeReferenceHandle)handle);
                return BuildFullName(reader, typeRef.Namespace, typeRef.Name);
            }

            if (handle.Kind == HandleKind.TypeDefinition)
            {
                TypeDefinition typeDef = reader.GetTypeDefinition((TypeDefinitionHandle)handle);
                return BuildFullName(reader, typeDef.Namespace, typeDef.Name);
            }

            return null;
        }

        /// <summary>
        /// Reads the simple names of every assembly referenced by the metadata.
        /// </summary>
        private static List<string> ReadReferencedAssemblyNames(MetadataReader reader)
        {
            List<string> names = new List<string>();

            foreach (AssemblyReferenceHandle handle in reader.AssemblyReferences)
            {
                AssemblyReference reference = reader.GetAssemblyReference(handle);
                names.Add(reader.GetString(reference.Name));
            }

            return names;
        }

        /// <summary>
        /// Combines a namespace handle and a name handle into a namespace-qualified full name. A type in the
        /// global namespace (empty namespace) yields just its name.
        /// </summary>
        private static string BuildFullName(MetadataReader reader, StringHandle namespaceHandle, StringHandle nameHandle)
        {
            string ns = reader.GetString(namespaceHandle);
            string name = reader.GetString(nameHandle);

            if (string.IsNullOrEmpty(ns))
            {
                return name;
            }

            return ns + "." + name;
        }
    }
}
