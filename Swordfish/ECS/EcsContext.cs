using System.Reflection;
using System.Collections.Generic;
using Swordfish.Rendering;
using Swordfish.Containers;
using Swordfish.Delegates;
using System;
using OpenTK.Mathematics;

namespace Swordfish.ECS
{
    public class ECSContext
    {
        public int EntityCount { get => _entityCount; }
        private int _entityCount = 0;

        private ExpandingList<Entity> _entities = new ExpandingList<Entity>();

        //  TODO create a preallocated chunk storage type for components
        private Dictionary<Type, ExpandingList<object>> _components = new Dictionary<Type, ExpandingList<object>>();

        private HashSet<ComponentSystem> _systems = new HashSet<ComponentSystem>();

        private Queue<int> _recycledIDs = new Queue<int>();

        public ECSContext()
        {
            _entities = new ExpandingList<Entity>();
            _components = new Dictionary<Type, ExpandingList<object>>();
            _systems = new HashSet<ComponentSystem>();
            _recycledIDs = new Queue<int>();

            Register();

            foreach (KeyValuePair<Type, ExpandingList<object>> pair in _components)
                Debug.Log($"{pair.Key.ToString()} > {pair.Value}");
        }

        public void Start()
        {
            foreach (ComponentSystem system in _systems)
                system.OnStart();
        }

        public void Step()
        {
            foreach (ComponentSystem system in _systems)
                system.OnUpdate();
        }

        public void Shutdown()
        {
            foreach (ComponentSystem system in _systems)
                system.OnShutdown();
        }

        /// <summary>
        /// Self-register all components and systems marked by attributes
        /// </summary>
        internal void Register()
        {
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();

            Debug.Log("Registering ECS components...");
            foreach (Type type in types)
            {
                if (Attribute.GetCustomAttribute(type, typeof(ComponentAttribute)) != null)
                {
                    if (RegisterComponent(Activator.CreateInstance(type)))
                        Debug.Log($"    Registered '{type}'");
                }
            }

            Debug.Log("Registering ECS systems...");
            ComponentSystemAttribute systemAttribute;
            foreach (Type type in types)
            {
                systemAttribute = (ComponentSystemAttribute)Attribute.GetCustomAttribute(type, typeof(ComponentSystemAttribute));

                if (systemAttribute != null)
                {
                    ComponentSystem system = (ComponentSystem)Activator.CreateInstance(type);
                    system.filter = systemAttribute.filter;

                    if (RegisterSystem(system))
                        Debug.Log($"    Registered '{type}'");
                }
            }
        }

        /// <summary>
        /// Registers a system of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>true if system was registered; otherwise false if system is already registered</returns>
        public bool RegisterSystem<T>() where T : ComponentSystem => _systems.Add( default(T) );

        /// <summary>
        /// Registers a system instance
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>true if system was registered; otherwise false if system is already registered</returns>
        public bool RegisterSystem(ComponentSystem system) => _systems.Add(system);

        /// <summary>
        /// Registers a component of type T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>true if component was registered; otherwise false if component is already registered</returns>
        public bool RegisterComponent<T>() where T : struct => _components.TryAdd(typeof(T), new ExpandingList<object>());

        /// <summary>
        /// Registers a component of type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>true if component was registered; otherwise false if component is already registered</returns>
        public bool RegisterComponent(object component) => _components.TryAdd(component.GetType(), new ExpandingList<object>());

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
        /// <returns>true if the push was sucessful; otherwise false if entity is already present in context</returns>
        public bool Push(Entity entity)
        {
            //  Attempt adding the entity
            if (_entities.TryAdd(entity))
            {
                //  Add an slot for the entity to every component
                foreach (KeyValuePair<Type, ExpandingList<object>> pair in _components)
                    if (_components.TryGetValue(pair.Key, out ExpandingList<object> data))
                        data.Add(null);

                //  Successfully pushed
                return true;
            }

            //  Failed to push
            return false;
        }

        /// <summary>
        /// Finds entities with provided components
        /// </summary>
        /// <param name="components"></param>
        /// <returns>array of matching entities; otherwise null if no entities are found</returns>
        public Entity[] Pull(params Type[] components)
        {
            List<Entity> foundEntities = new List<Entity>();

            //  ! Slow brute force method of finding matching components
            int matches = 0;
            foreach (Entity entity in _entities)
            {
                if (entity == null) continue;

                matches = 0;
                foreach (Type type in components)
                {
                    if (_components.TryGetValue(type, out ExpandingList<object> data))
                    {
                        if (data[entity.UID] != null)
                            matches++;
                    }
                }

                if (matches == components.Length)
                    foundEntities.Add(entity);
            }

            return foundEntities.ToArray();
        }

        /// <summary>
        /// Gets component data from an entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns>component if it exists on entity; otherwise returns dummy default</returns>
        public T Get<T>(Entity entity) where T : struct
        {
            if (_components.TryGetValue(typeof(T), out ExpandingList<object> data))
                return (T)data[entity.UID];

            return default(T);
        }

        /// <summary>
        /// Creates and pushes an entity to the context
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <returns>the created entity; otherwise null if pushing to context fails</returns>
        public Entity CreateEntity(string name = "", string tag = "")
        {
            Entity entity = new Entity(this, name, tag, CreateEntityUID());

            //  Try pushing to context
            if (Push(entity))
                _entityCount++;
            //  Otherwise recycle
            else
            {
                _recycledIDs.Enqueue(entity.UID);
                Debug.Log("CreateEntity failed to push context", "ECS", LogType.WARNING);
                return null;
            }

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
            _entityCount--;

            //  Release component data
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
            if (!_components.ContainsKey(component.GetType()))
                Debug.Log($"Component of type({component.GetType().ToString()}) is not registered to context", "ECS", LogType.WARNING);

            if (_components.TryGetValue(component.GetType(), out ExpandingList<object> data))
            {
                data[entity.UID] = component;

                //  Tell all systems with this component to update matching entities
                foreach (ComponentSystem system in _systems)
                    if (system.IsFiltering(component.GetType()))
                        system.PullEntities();
            }

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
            if (!_components.ContainsKey(typeof(T)))
                Debug.Log($"Component of type({typeof(T).ToString()}) is not registered to context", "ECS", LogType.WARNING);

            if (_components.TryGetValue(typeof(T), out ExpandingList<object> data))
            {
                data[entity.UID] = null;

                //  Tell all systems with this component to update matching entities
                foreach (ComponentSystem system in _systems)
                    if (system.IsFiltering(typeof(T)))
                        system.PullEntities();

            }

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
            if (!_components.ContainsKey(typeof(T)))
                Debug.Log($"Component of type({typeof(T).ToString()}) is not registered to context", "ECS", LogType.WARNING);

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
        public ECSContext Do<T>(Entity entity, ReturnAction<T> action) where T : struct
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
