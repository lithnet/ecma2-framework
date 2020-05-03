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

        public static IEnumerable<Type> GetPluginsOfType<T>()
        {
            try
            {
                return Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetInterfaces().Contains(typeof(T)));
            }
            catch (ReflectionTypeLoadException ex)
            {
                logger.Error("Unable to load interfaces from type",ex);

                foreach (var e in ex.LoaderExceptions)
                {
                    logger.Error(e);
                }

                throw;
            }
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
