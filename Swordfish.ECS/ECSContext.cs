namespace Swordfish.ECS;

public class ECSContext(byte chunkBitWidth = 16)
{
    private readonly object _systemsLock = new();
    private readonly List<EntitySystem> _systems = [];

    public readonly DataStore DataStore = new(chunkBitWidth);

    public bool AddSystem<T>() where T : EntitySystem
    {
        lock (_systemsLock)
        {
            if (_systems.OfType<T>().Any())
            {
                return false;
            }

            var system = (EntitySystem)Activator.CreateInstance(typeof(T), DataStore)!;
            _systems.Add(system);
            return true;
        }
    }

    public void Tick()
    {
        lock (_systemsLock)
        {
            Parallel.ForEach(_systems, ForEachSystem);
        }
    }

    private static void ForEachSystem(EntitySystem system, ParallelLoopState state, long index)
    {
        system.Tick();
    }
}