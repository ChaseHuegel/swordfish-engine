using System;
using System.Reflection;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Swordfish.ECS
{
    public class Component
    {
        private static ConcurrentDictionary<Type, ushort> componentToId = new ConcurrentDictionary<Type, ushort>();
        private static ushort currentId = 0;

        /// <summary>
        /// Register valid component types
        /// </summary>
        public static void RegisterComponents()
        {
            Debug.Log("Registering ECS components...");

            //  Use reflection to register systems and components marked by attributes
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                if (Attribute.GetCustomAttribute(type, typeof(ComponentAttribute)) != null)
                {
                    Component.Register(type);
                    Debug.Log($"    Registered '{type}' as {currentId-1}");
                }
            }
        }

        /// <summary>
        /// Register a component of type, ignoring duplicates
        /// </summary>
        /// <param name="type"></param>
        /// <returns>component id</returns>
        private static ushort Register(Type type)
        {
            componentToId.TryAdd(type, currentId);
            currentId++;

            return (ushort)(currentId-1);
        }

        /// <summary>
        /// Get id of component type T or registers if not present
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>component id</returns>
        public static ushort Get<T>() where T : struct
        {
            if (componentToId.TryGetValue(typeof(T), out ushort value))
                return value;

            return Register(typeof(T));
        }

        /// <summary>
        /// Get id of component from object type or registers if not present
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>component id</returns>
        public static ushort Get(object obj)
        {
            if (componentToId.TryGetValue(obj.GetType(), out ushort value))
                return value;

            return Register(obj.GetType());
        }

        /// <summary>
        /// Get type of component from id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>component type</returns>
        public static Type GetType(ushort id)
        {
            foreach (KeyValuePair<Type, ushort> pair in componentToId)
                if (pair.Value == id)
                    return pair.Key;

            return null;
        }
    }
}
