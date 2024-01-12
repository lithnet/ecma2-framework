using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog;

namespace Lithnet.Ecma2Framework
{
    internal static class InterfaceManager
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private static Assembly ecmaAssembly;

        public static IEnumerable<Type> GetPluginsOfType<T>()
        {
            try
            {
                ecmaAssembly ??= FindEcmaAssembly();
                if (ecmaAssembly == null)
                {
                    throw new Lithnet.Ecma2Framework.ProviderNotFoundException("The EcmaAssemblyAttribute was not found on any loaded assembly");
                }

                return ecmaAssembly.GetTypes().Where(t => t.GetInterfaces().Contains(typeof(T)) && !t.IsAbstract);
            }
            catch (ReflectionTypeLoadException ex)
            {
                logger.Error("Unable to load interfaces from type", ex);

                foreach (var e in ex.LoaderExceptions)
                {
                    logger.Error(e);
                }

                throw;
            }
        }

        private static Assembly FindEcmaAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == Assembly.GetExecutingAssembly())
                {
                    continue;
                }

                if (Attribute.IsDefined(assembly, typeof(EcmaAssemblyAttribute)))
                {
                    return assembly;
                }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetExportedTypes().Where(t => t.IsPublic && !t.IsAbstract))
                {
                    if (typeof(Microsoft.MetadirectoryServices.IMAExtensible2GetSchema).IsAssignableFrom(type))
                    {
                        return assembly;
                    }
                }
            }

            return null;
        }

        public static IEnumerable<T> GetInstancesOfType<T>()
        {
            foreach (Type t in InterfaceManager.GetPluginsOfType<T>())
            {
                yield return (T)Activator.CreateInstance(t);
            }
        }

        public static T GetProviderOrDefault<T>()
        {
            return InterfaceManager.GetInstancesOfType<T>().FirstOrDefault();
        }

        public static T GetProviderOrThrow<T>()
        {
            T provider = InterfaceManager.GetInstancesOfType<T>().FirstOrDefault();

            if (provider == null)
            {
                throw new ProviderNotFoundException($"A provider for type {typeof(T).FullName} was not found");
            }

            return provider;
        }
    }
}
