using System.ComponentModel;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Swordfish.ECS
{
    public class Entity
    {
        public EcsContext Context = null;
        public ushort ID = 0;

        public Entity() { }

        public Entity(ushort id, EcsContext context)
        {
            this.ID = id;
            this.Context = context;
        }

        private ConcurrentDictionary<ushort, object> components = new ConcurrentDictionary<ushort, object>();

        public void PushComponents()
        {
            if (Context != null)
                foreach (KeyValuePair<ushort, object> pair in components)
                    Context.PushComponent(this, pair.Key);
        }

        public bool HasComponents(BitMask filter)
        {
            List<ushort> componentIDs = new List<ushort>();

            //  Convert BitMask indicies to componentIDs
            for (int i = 0; i < filter.Length; i++)
                if (filter[i])
                    componentIDs.Add((ushort)i);

            return HasComponents(componentIDs.ToArray());
        }

        public bool HasComponents(params Type[] components)
        {
            ushort[] componentIDs = new ushort[components.Length];

            for (int i = 0; i < componentIDs.Length; i++)
                componentIDs[i] = Component.Get(components[i]);

            return HasComponents(componentIDs);
        }

        public bool HasComponents(params ushort[] componentIDs)
        {
            //  Grab the list of entities containing each component
            for (int i = 0; i < componentIDs.Length; i++)
                if (!components.ContainsKey(componentIDs[i]))
                    return false;

            return true;
        }

        /// <summary>
        /// Sets component data or adds if not present
        /// </summary>
        /// <param name="component"></param>
        /// <returns>this entity for building</returns>
        public Entity SetData<T>(object data) where T : struct
        {
            components.AddOrUpdate(Component.Get<T>(), data, (k, v) => v = data);
            Context?.PushComponent(this, Component.Get<T>());

            return this;
        }

        /// <summary>
        /// Gets component data of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>null if component of type T does not exist</returns>
        public T GetData<T>() where T : struct
        {
            if (components.TryGetValue(Component.Get<T>(), out object value))
                return (T)value;

            return default(T);
        }

        /// <summary>
        /// Destroy this entity
        /// </summary>
        public void Destroy()
        {
            Context?.DestroyEntity(this);
        }
    }
}
