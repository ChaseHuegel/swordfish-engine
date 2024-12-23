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
            return true;
        }

        bool SystemTypeComparer(IEntitySystem other)
        {
            return system.GetType() == other.GetType();
        }
    }

    public void Tick(float delta)
    {
        lock (_systemsLock)
        {
            //  TODO Should this stay parallel? Makes debugging harder and won't allow ordering system execution.
            Parallel.ForEach(_systems, ParallelTick);
        }

        void ParallelTick(IEntitySystem system, ParallelLoopState state, long index)
        {
            system.Tick(delta, DataStore);
        }
    }
}