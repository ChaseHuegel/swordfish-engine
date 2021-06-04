using System.Reflection;
using System.Collections.Generic;
using Swordfish.Containers;
using Swordfish.Delegates;
using System;
using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    //  ! Testing
    struct PositionComponent
    {
        public Vector3 position;
    }

    //  ! Testing
    struct RotationComponent
    {
        public Quaternion orientation;
    }

    public class ECSContext
    {
        private ExpandingList<Entity> _entities = new ExpandingList<Entity>();
        private Dictionary<Type, ExpandingList<object>> _components = new Dictionary<Type, ExpandingList<object>>();
        private List<ComponentSystem> _systems = new List<ComponentSystem>();

        private Queue<int> _recycledIDs = new Queue<int>();

        public ECSContext()
        {
            // ! Test case
            Entity entity = CreateEntity("Test");
            Attach<PositionComponent>(entity, new PositionComponent())
            .Attach<RotationComponent>(entity, new RotationComponent())
            .Worker<PositionComponent>(entity, x => {
                    x.position += Vector3.One;
                    return x;
                });
        }

        /// <summary>
        /// Creates a new or recycled UID
        /// </summary>
        /// <returns>the UID</returns>
        internal int CreateEntityUID()
        {
            if (_recycledIDs.Count > 0)
                return _recycledIDs.Dequeue();

            return _entities.Count;
        }

        /// <summary>
        /// Pushes an entity to the context, ignoring duplicates
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>true if the push was sucessful</returns>
        public bool Push(Entity entity) => _entities.TryAdd(entity);

        /// <summary>
        /// Creates and pushes an entity to the context
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <returns>entity builder</returns>
        public Entity CreateEntity(string name = "", string tag = "")
        {
            Entity entity = new Entity(this, name, tag);

            //  Try pushing to context
            if (!Push(entity)) Debug.Log("Entity already present in context", "ECS", LogType.WARNING);

            return entity;
        }

        /// <summary>
        /// Remove an entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>entity builder</returns>
        public ECSContext DestroyEntity(Entity entity)
        {
            //  Release the entity
            _entities[entity.UID] = null;

            //  Release components
            foreach (KeyValuePair<Type, ExpandingList<object>> pair in _components)
                if (_components.TryGetValue(pair.Key, out ExpandingList<object> data))
                    data[entity.UID] = null;

            //  Recycle the UID
            _recycledIDs.Enqueue(entity.UID);

            return this;
        }

        //  TODO move all of these into an actual builder

        /// <summary>
        /// Attaches a component to entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="component"></param>
        /// <returns>entity builder</returns>
        public ECSContext Attach<T>(Entity entity, T component) where T : struct
        {
            if (_components.TryGetValue(component.GetType(), out ExpandingList<object> data))
                data[entity.UID] = component;

            return this;
        }

        /// <summary>
        /// Detach a component from entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns>entity builder</returns>
        public ECSContext Detach<T>(Entity entity) where T : struct
        {
            if (_components.TryGetValue(typeof(T), out ExpandingList<object> data))
                data[entity.UID] = null;

            return this;
        }

        /// <summary>
        /// Set component data
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="component"></param>
        /// <returns>entity builder</returns>
        public ECSContext Set<T>(Entity entity, T component) where T : struct
        {
            if (_components.TryGetValue(component.GetType(), out ExpandingList<object> data))
                data[entity.UID] = component;

            return this;
        }

        /// <summary>
        /// Run an action using a component
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="component"></param>
        /// <returns>entity builder</returns>
        public ECSContext Worker<T>(Entity entity, ReturnAction<T> action) where T : struct
        {
            T component;

            if (_components.TryGetValue(typeof(T), out ExpandingList<object> data))
                component = (T)data[entity.UID];
            else
                component = default(T);

            data[entity.UID] = action(component);

            return this;
        }
    }
}
