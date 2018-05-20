using System;
using System.Collections.Generic;
using System.Linq;

namespace Lithnet.Ecma2Framework
{
    internal static class InterfaceManager
    {
        public static IEnumerable<Type> GetPluginsOfType<T>()
        {
            return System.Reflection.Assembly.GetExecutingAssembly().GetTypes().Where(t => t.GetInterfaces().Contains(typeof(T)));
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
