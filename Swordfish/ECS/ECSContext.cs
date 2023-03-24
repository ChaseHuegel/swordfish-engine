using System.Runtime.CompilerServices;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Reflection;
using Swordfish.Library.Threading;

namespace Swordfish.ECS;

public class ECSContext : IECSContext
{
    private const string REQ_START_MESSAGE = "The context must be started.";
    private const string REQ_STOP_MESSAGE = "The context must be stopped.";

    public const int DEFAULT_MAX_ENTITIES = 128000;

    public int MaxEntities { get; }

    public EntityBuilder EntityBuilder
    {
        get
        {
            if (!Running)
                throw new NullReferenceException(REQ_START_MESSAGE);

            return _EntityBuilder ??= new EntityBuilder(this, Store);
        }
    }

    public ChunkedDataStore Store { get; private set; }

    private ThreadWorker ThreadWorker { get; set; }

    internal bool Modified;

    private bool Running;
    private readonly IndexLookup<Type> ComponentTypes;
    private readonly HashSet<ComponentSystem> Systems;
    private EntityBuilder? _EntityBuilder;

    public ECSContext()
    {
        Debugger.Log($"Initializing ECS context.");

        Running = false;
        Modified = false;
        MaxEntities = DEFAULT_MAX_ENTITIES;

        Store = new ChunkedDataStore(0, 1);
        ComponentTypes = new IndexLookup<Type>();
        Systems = new HashSet<ComponentSystem>();

        BindComponent<IdentifierComponent>();
        BindComponent<TransformComponent>();
        BindComponent<PhysicsComponent>();
        BindComponent<MeshRendererComponent>();

        BindSystem<PhysicsSystem>();
        BindSystem<MeshRendererSystem>();

        ThreadWorker = new ThreadWorker(Update, "ECS");
    }

    public void Start()
    {
        if (Running)
            throw new InvalidOperationException(REQ_STOP_MESSAGE);

        Debugger.Log($"Starting ECS context.");

        Store = new ChunkedDataStore(MaxEntities, ComponentTypes.Count);
        Running = true;

        ThreadWorker.Start();
    }

    public void Stop()
    {
        Debugger.Log($"Stopping ECS context.");
        Running = false;
        ThreadWorker?.Stop();
    }

    public void Reset()
    {
        Store = new ChunkedDataStore(MaxEntities, ComponentTypes.Count);
        Modified = true;
    }

    public void Update(float deltaTime)
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetComponentIndex(Type type) => ComponentTypes.IndexOf(type);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetComponentIndex<TComponent>() => ComponentTypes.IndexOf(typeof(TComponent));

    public int BindComponent<TComponent>()
    {
        if (Running)
            throw new InvalidOperationException(REQ_STOP_MESSAGE);

        Debugger.Log($"Binding ECS component '{typeof(TComponent)}'.");

        if (!Reflection.HasAttribute<TComponent, ComponentAttribute>())
            throw new ArgumentException($"Type '{typeof(TComponent)}' must be decorated as a Component.");

        if (ComponentTypes.Contains(typeof(TComponent)))
            throw new InvalidOperationException($"Component of type '{typeof(TComponent)}' is already bound.");

        ComponentTypes.Add(typeof(TComponent));

        return ComponentTypes.Count - 1;
    }

    public TSystem BindSystem<TSystem>() where TSystem : ComponentSystem
    {
        if (Running)
            throw new InvalidOperationException(REQ_STOP_MESSAGE);

        Debugger.Log($"Binding ECS system '{typeof(TSystem)}'.");

        if (Activator.CreateInstance(typeof(TSystem)) is not ComponentSystem system)
            throw new NullReferenceException();

        if (Reflection.TryGetAttribute<TSystem, ComponentSystemAttribute>(out ComponentSystemAttribute attribute))
            system.Filter = attribute.Filter;

        Systems.Add(system);

        return (TSystem)system;
    }

    public Entity CreateEntity()
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

        int ptr = Store.Add();
        Modified = true;
        return new Entity(ptr, this);
    }

    public Entity CreateEntity(object?[] components)
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

        int ptr = Store.Add(components);
        Modified = true;
        return new Entity(ptr, this);
    }

    public void RemoveEntity(Entity entity)
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

        Store.Remove(entity.Ptr);
        Modified = true;
    }

    public Entity[] GetEntities()
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

        int[] ptrs = Store.All();
        Entity[] entities = new Entity[ptrs.Length];
        for (int i = 0; i < entities.Length; i++)
            entities[i] = new Entity(ptrs[i], this);

        return entities;
    }

    public Entity[] GetEntities(params Type[] componentTypes)
    {
        if (!Running)
            throw new InvalidOperationException(REQ_START_MESSAGE);

        int[] ptrs = Store.All();
        Entity[] entities = new Entity[ptrs.Length];
        int entityCount = 0;

        for (int i = 0; i < ptrs.Length; i++)
        {
            int componentsFound = 0;

            for (int n = 0; n < componentTypes.Length; n++)
                if (Store.HasAt(ptrs[i], GetComponentIndex(componentTypes[n])))
                    componentsFound++;

            if (componentsFound == componentTypes.Length)
                entities[entityCount++] = new Entity(ptrs[i], this);
        }

        Entity[] results = new Entity[entityCount];
        Array.Copy(entities, 0, results, 0, entityCount);
        return results;
    }
}
