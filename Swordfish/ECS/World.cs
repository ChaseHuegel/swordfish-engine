using Swordfish.Library.Diagnostics;
using Swordfish.Library.Types;
using Swordfish.Library.Util;

namespace Swordfish.ECS;

public partial class World
{
    private const string ReqInitializedMessage = "The world must be itialized.";
    private const string ReqUnitializedMessage = "The world is already initialized.";

    public const int DefaultMaxEntities = 128000;

    public int MaxEntities { get; }

    public EntityBuilder EntityBuilder
    {
        get
        {
            if (!Initialized)
                throw new NullReferenceException(ReqInitializedMessage);

            return _EntityBuilder ??= new EntityBuilder(this, Store);
        }
    }

    internal bool Modified;
    internal ChunkedDataStore Store { get; private set; }

    private bool Initialized;
    private readonly Dictionary<Type, int> ComponentTypes;
    private readonly HashSet<ComponentSystem> Systems;
    private EntityBuilder? _EntityBuilder;

    public World(int maxEntities = DefaultMaxEntities)
    {
        Initialized = false;
        Modified = false;
        MaxEntities = maxEntities;

        Store = new ChunkedDataStore(0, 1);
        ComponentTypes = new Dictionary<Type, int>();
        Systems = new HashSet<ComponentSystem>();

        BindComponent<IdentifierComponent>();
    }

    public void Initialize()
    {
        Initialized = true;
        Store = new ChunkedDataStore(MaxEntities, ComponentTypes.Count);
    }

    public void Update(float deltaTime)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        foreach (ComponentSystem system in Systems)
        {
            if (Modified)
            {
                system.Modified = true;
                Modified = false;
            }

            system.Update(this, deltaTime);
        }
    }

    public int GetComponentIndex(Type type) => ComponentTypes[type];

    public int GetComponentIndex<TComponent>() => ComponentTypes[typeof(TComponent)];

    public int BindComponent<TComponent>()
    {
        if (Initialized)
            throw new InvalidOperationException(ReqUnitializedMessage);

        if (!Reflection.HasAttribute<TComponent, ComponentAttribute>())
            throw new ArgumentException($"Type {typeof(TComponent)} must be decorated as a Component.");

        if (ComponentTypes.TryGetValue(typeof(TComponent), out _))
            throw new InvalidOperationException($"Component of type {typeof(TComponent)} is already bound.");

        ComponentTypes.Add(typeof(TComponent), ComponentTypes.Count);
        Debugger.Log($"Bound component {typeof(TComponent)}.");

        return ComponentTypes.Count - 1;
    }

    public void BindSystem<TSystem>() where TSystem : ComponentSystem
    {
        if (Initialized)
            throw new InvalidOperationException(ReqUnitializedMessage);

        if (Activator.CreateInstance(typeof(TSystem)) is not ComponentSystem system)
            throw new NullReferenceException();

        if (Reflection.TryGetAttribute<TSystem, ComponentSystemAttribute>(out ComponentSystemAttribute attribute))
            system.Filter = attribute.Filter;

        Systems.Add(system);

        Debugger.Log($"Bound system {typeof(TSystem)}.");
    }

    public Entity CreateEntity()
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        Modified = true;
        return new Entity(Store.Add(), this);
    }

    public Entity CreateEntity(object?[] components)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        int ptr = Store.Add(components);
        Modified = true;
        return new Entity(ptr, this);
    }

    public void RemoveEntity(Entity entity)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        Modified = true;
        Store.Remove(entity.Ptr);
    }

    public Entity[] GetEntities()
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        int[] ptrs = Store.All();
        Entity[] entities = new Entity[ptrs.Length];
        for (int i = 0; i < entities.Length; i++)
            entities[i] = new Entity(ptrs[i], this);

        return entities;
    }

    public Entity[] GetEntities(params Type[] componentTypes)
    {
        if (!Initialized)
            throw new InvalidOperationException(ReqInitializedMessage);

        int[] ptrs = Store.All();
        Entity[] entities = new Entity[ptrs.Length];
        int entityCount = 0;

        for (int i = 0; i < ptrs.Length; i++)
        {
            for (int n = 0; n < componentTypes.Length; n++)
            {
                if (Store.HasAt(ptrs[i], GetComponentIndex(componentTypes[n])))
                {
                    entities[entityCount++] = new Entity(ptrs[i], this);
                    break;
                }
            }
        }

        Entity[] results = new Entity[entityCount];
        Array.Copy(entities, 0, results, 0, entityCount);
        return results;
    }
}
