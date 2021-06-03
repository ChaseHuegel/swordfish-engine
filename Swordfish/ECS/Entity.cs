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

        /// <summary>
        /// Sets component data or adds if not present
        /// </summary>
        /// <param name="component"></param>
        /// <returns>this entity for building</returns>
        public Entity SetData<T>(object data) where T : struct
        {
            components.AddOrUpdate(Component.Get<T>(), data, (k, v) => v = data);

            return this;
        }

        /// <summary>
        /// Gets component data of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>null if component of type T does not exist</returns>
        public T? GetData<T>() where T : struct
        {
            if (components.TryGetValue(Component.Get<T>(), out object value))
                return (T)value;

            return null;
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
