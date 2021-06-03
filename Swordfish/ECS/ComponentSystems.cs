using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Swordfish.ECS
{
    public class ComponentSystems
    {
        private static HashSet<Type> systemTypes = new HashSet<Type>();
        private static ConcurrentDictionary<Type, BitMask> systemMasks = new ConcurrentDictionary<Type, BitMask>();

        /// <summary>
        /// Register valid system types
        /// </summary>
        public static void RegisterSystems()
        {
            Debug.Log("Registering ECS systems...");

            //  Use reflection to register systems and components marked by attributes
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            ComponentSystemAttribute attribute;
            foreach (Type type in types)
            {
                attribute = (ComponentSystemAttribute)Attribute.GetCustomAttribute(type, typeof(ComponentSystemAttribute));

                if (attribute != null && type.GetInterfaces().Contains(typeof(IComponentSystem)))
                {
                    Register(type);

                    //  Create a bitmask of the components this system interacts with
                    BitMask mask = new BitMask();
                    foreach (Type maskedComponent in attribute.mask)
                        mask.Set( Component.Get(maskedComponent) );

                    systemMasks.TryAdd(type, mask);

                    Debug.Log($"    Registered '{type}'");
                }
            }
        }

        /// <summary>
        /// Register a system of type, ignoring duplicates
        /// </summary>
        /// <param name="system"></param>
        private static void Register(Type system)
        {
            lock (systemTypes) systemTypes.Add(system);
        }

        public EcsContext Context;
        private HashSet<IComponentSystem> systems = new HashSet<IComponentSystem>();

        public ComponentSystems(EcsContext Context)
        {
            this.Context = Context;

            //  Create systems from registered systems
            foreach (Type type in systemTypes)
                systems.Add( (IComponentSystem)Activator.CreateInstance(type) );
        }

        /// <summary>
        /// Call Start on systems
        /// </summary>
        public void Start()
        {
            foreach (IComponentSystem s in systems)
                s.Start();
        }

        /// <summary>
        /// Call Update on systems
        /// </summary>
        public void Update()
        {
            foreach (IComponentSystem s in systems)
                s.Update();
        }

        /// <summary>
        /// Call Destroy on systems
        /// </summary>
        public void Destroy()
        {
            foreach (IComponentSystem s in systems)
                s.Destroy();
        }
    }
}
