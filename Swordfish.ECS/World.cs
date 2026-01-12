namespace Swordfish.ECS;

public class World(in byte chunkBitWidth = 16)
{
    private readonly object _systemsLock = new();
    private readonly List<IEntitySystem> _systems = [];

    public readonly DataStore DataStore = new(chunkBitWidth);

    public Entity NewEntity()
    {
        return new Entity(DataStore.Alloc(), DataStore);
    }

    // ReSharper disable once UnusedMethodReturnValue.Global
    public bool AddSystem(IEntitySystem system)
    {
        lock (_systemsLock)
        {
            if (_systems.Any(SystemTypeComparer))
            {
                return false;
            }

            _systems.Add(system);
            _systems.Sort(Comparison);
            return true;
        }

        bool SystemTypeComparer(IEntitySystem other)
        {
            return system.GetType() == other.GetType();
        }
    }

    private int Comparison(IEntitySystem x, IEntitySystem y)
    {
        if (x.Order < y.Order)
        {
            return -1;
        }
        
        if (x.Order > y.Order)
        {
            return 1;
        }

        return 0;
    }

    public void Tick(float delta)
    {
        lock (_systemsLock)
        {
            foreach (IEntitySystem system in _systems)
            {
                system.Tick(delta, DataStore);
            }
        }
    }
}