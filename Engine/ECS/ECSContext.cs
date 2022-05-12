using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

using Swordfish.Library.Containers;
using Swordfish.Delegates;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Threading;

namespace Swordfish.Engine.ECS
{
    public class ECSContext
    {
        public int EntityCount { get => _entityCount; }
        private int _entityCount = 0;

        private Queue<int> _recycledIDs;
        private ConcurrentBag<Entity> _awaitingDestroy;

        //  TODO create a preallocated chunk storage type for components
        private ConcurrentDictionary<Type, ExpandingList<object>> _components;
        private ExpandingList<Entity> _entities;
        private HashSet<ComponentSystem> _systems;

        public float ThreadTime = 0f;
        private float[] times = new float[6];
        private int timeIndex = 0;
        private float timer = 0f;

        public readonly ThreadWorker Thread;

        public ECSContext()
        {
            _entities = new ExpandingList<Entity>();
            _components = new ConcurrentDictionary<Type, ExpandingList<object>>();
            _systems = new HashSet<ComponentSystem>();
            _recycledIDs = new Queue<int>();
            _awaitingDestroy = new ConcurrentBag<Entity>();

            Thread = new ThreadWorker(Step, false, "ECS");
        }

        public void Start()
        {
            //  Register components and systems
            Register();

            foreach (ComponentSystem system in _systems)
                system.OnStart();

            Thread.Start();
        }

        public void Shutdown()
        {
            foreach (ComponentSystem system in _systems)
                system.OnShutdown();

            Thread.Stop();
        }

        public void Step(float deltaTime)
        {
            //  Apply timescale
            deltaTime *= Swordfish.Timescale;

            //  Destroy marked entities
            foreach (Entity entity in _awaitingDestroy)
                Destroy(entity);
            _awaitingDestroy.Clear();

            //  Update systems
            foreach (ComponentSystem system in _systems)
                system.Update(deltaTime);

            //  TODO: Very quick and dirty stable timing
            timer += deltaTime;
            times[timeIndex] = deltaTime;
            timeIndex++;
            if (timeIndex >= times.Length)
                timeIndex = 0;
            if (timer >= 1f/times.Length)
            {
                timer = 0f;

                float highest = 0f;
                float lowest = 9999f;
                ThreadTime = 0f;
                foreach (float timing in times)
                {
                    ThreadTime += timing;
                    if (timing <= lowest) lowest = timing;
                    if (timing >= highest) highest = timing;
                }

                ThreadTime -= lowest;
                ThreadTime -= highest;
                ThreadTime /= (times.Length - 2);
            }
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
                        Debug.Log($"Registered '{type}'", LogType.CONTINUED);
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
                        Debug.Log($"Registered '{type}'", LogType.CONTINUED);
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

            //  ! Entity ID starts at 1, indicies start at 0
            //  ID = 0 is a 'null' entity
            return _entities.Count + 1;
        }

        /// <summary>
        /// Pushes an entity to the context, ignoring duplicates
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>true if the push was sucessful; otherwise false if entity is already present in context</returns>
        public bool Push(Entity entity)
        {
            //  Push to a recycled slot if available
            if (entity.UID < _entities.Count && _entities[entity.UID-1] == null)
            {
                _entities[entity.UID-1] = entity;

                //  Clear component data
                foreach (KeyValuePair<Type, ExpandingList<object>> pair in _components)
                    if (_components.TryGetValue(pair.Key, out ExpandingList<object> data))
                        data[entity.UID-1] = null;

                return true;
            }

            //  ...Otherwise attempt pushing to the list
            if (_entities.TryAdd(entity))
            {
                //  Add component data
                foreach (KeyValuePair<Type, ExpandingList<object>> pair in _components)
                    if (_components.TryGetValue(pair.Key, out ExpandingList<object> data))
                        data.Add(null);

                return true;
            }

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
                        if (data[entity.UID-1] != null)
                            matches++;
                    }
                }

                if (matches == components.Length)
                    foundEntities.Add(entity);
            }

            return foundEntities.ToArray();
        }

        /// <summary>
        /// Finds pointers to entities with provided components
        /// </summary>
        /// <param name="components"></param>
        /// <returns>array of matching entity pointers; otherwise null if no entities are found</returns>
        public int[] PullPtr(params Type[] components)
        {
            List<int> foundEntities = new List<int>();

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
                        if (data[entity.UID-1] != null)
                            matches++;
                    }
                }

                if (matches == components.Length)
                    foundEntities.Add(entity.UID);
            }

            return foundEntities.ToArray();
        }

        /// <summary>
        /// Checks for component data on an entity
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns>component if it exists on entity; otherwise returns dummy default</returns>
        public bool HasComponent<T>(Entity entity) where T : struct
        {
            if (_components.TryGetValue(typeof(T), out ExpandingList<object> data))
                return data[entity.UID-1] != null;

            return false;
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
            {
                if (data[entity.UID-1] == null)
                    return default(T);
                else
                    return (T)data[entity.UID-1];
            }

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
                Interlocked.Increment(ref _entityCount);
            //  Otherwise recycle
            else
            {
                _recycledIDs.Enqueue(entity.UID);
                Debug.Log("CreateEntity failed to push context", "ECS", LogType.ERROR);
                return null;
            }

            return entity;
        }

        /// <summary>
        /// Creates an entity with attached components, and pushes to the context
        /// </summary>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <param name="components"></param>
        /// <returns>the created entity; otherwise null if pushing to context failed</returns>
        public Entity CreateEntity(string name = "", string tag = "", params object[] components)
        {
            Entity entity = CreateEntity(name, tag);

            if (entity != null)
                Attach(entity, components);

            return entity;
        }

        /// <summary>
        /// Creates and pushes an entity to the context
        /// </summary>
        /// <param name="entity">the created entity; otherwise null if pushing to context failed</param>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <returns>true if an entity was created; otherwise false</returns>
        public bool CreateEntity(out Entity entity, string name = "", string tag = "")
        {
            entity = CreateEntity(name, tag);

            return entity != null;
        }

        /// <summary>
        /// Creates an entity with attached components, and pushes to the context
        /// </summary>
        /// <param name="entity">the created entity; otherwise null if pushing to context failed</param>
        /// <param name="name"></param>
        /// <param name="tag"></param>
        /// <param name="components"></param>
        /// <returns>true if an entity was created; otherwise false</returns>
        public bool CreateEntity(out Entity entity, string name = "", string tag = "", params object[] components)
        {
            entity = CreateEntity(name, tag);

            if (entity != null)
                Attach(entity, components);

            return entity != null;
        }

        /// <summary>
        /// Mark an entity to be removed from context
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>entity builder</returns>
        public ECSContext DestroyEntity(Entity entity)
        {
            _awaitingDestroy.Add(entity);

            return this;
        }

        /// <summary>
        /// Remove an entity from context
        /// </summary>
        /// <param name="entity"></param>
        /// <returns>entity builder</returns>
        private ECSContext Destroy(Entity entity)
        {
            //  Recycle the UID
            _recycledIDs.Enqueue(entity.UID);

            //  Release the entity
            _entities[entity.UID-1] = null;
            Interlocked.Decrement(ref _entityCount);

            //  Collect a list of all components this entity had
            List<Type> destroyedComponents = new List<Type>();
            foreach (KeyValuePair<Type, ExpandingList<object>> pair in _components)
                if (_components.TryGetValue(pair.Key, out ExpandingList<object> data) && data[entity.UID-1] != null)
                    destroyedComponents.Add(pair.Key);

            //  Tell all systems with this entity's components to do a fresh pull
            foreach (ComponentSystem system in _systems)
                if (system.IsFiltering(destroyedComponents.ToArray()))
                    system.PullEntities();

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
                data[entity.UID-1] = component;

                //  Tell all systems with this component to update matching entities
                foreach (ComponentSystem system in _systems)
                    if (system.IsFiltering(component.GetType()))
                        system.PullEntities();
            }

            return this;
        }

        /// <summary>
        /// Attaches a collection of components to an entity
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="components"></param>
        /// <returns>entity builder</returns>
        public ECSContext Attach(Entity entity, params object[] components)
        {
            foreach (object component in components)
            {
                //  Ignore null values and anything that is not a struct
                if (component == null || !component.GetType().IsValueType) continue;

                if (!_components.ContainsKey(component.GetType()))
                    Debug.Log($"Component of type({component.GetType().ToString()}) is not registered to context", "ECS", LogType.WARNING);

                if (_components.TryGetValue(component.GetType(), out ExpandingList<object> data))
                {
                    data[entity.UID-1] = component;

                    //  Tell all systems with this component to update matching entities
                    foreach (ComponentSystem system in _systems)
                        if (system.IsFiltering(component.GetType()))
                            system.PullEntities();
                }
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
                data[entity.UID-1] = null;

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
                data[entity.UID-1] = component;

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
                component = data[entity.UID-1] != null ? (T)data[entity.UID-1] : default(T);
            else
                return this;

            data[entity.UID-1] = action(component);

            return this;
        }
    }
}
