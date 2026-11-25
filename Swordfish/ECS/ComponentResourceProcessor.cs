using Krill;

namespace Swordfish.ECS;

/// <summary>Drives resource release from removed-component queries for one component type.</summary>
/// <typeparam name="T">The component type whose removals are handled.</typeparam>
public abstract class ComponentResourceProcessor<T> : IComponentResourceProcessor
    where T : struct, IDataComponent
{
    /// <inheritdoc/>
    public void Process(DataStore store, float delta)
    {
        ProcessLive(store, delta);

        var action = new RemovedAction(this);
        store.QueryRemoved<T, RemovedAction>(delta, ref action);

        ProcessRetirements(store, delta);
    }

    /// <summary>Observes live components before removals are handled.</summary>
    protected virtual void ProcessLive(DataStore store, float delta)
    {
    }

    /// <summary>Handles one removed component. The base acknowledges the removal afterwards.</summary>
    protected abstract void OnRemoved(in T component, int entity);

    /// <summary>Runs after removals are handled.</summary>
    protected virtual void ProcessRetirements(DataStore store, float delta)
    {
    }

    private readonly struct RemovedAction : IForEach<T>
    {
        private readonly ComponentResourceProcessor<T> _owner;

        public RemovedAction(ComponentResourceProcessor<T> owner)
        {
            _owner = owner;
        }

        public void Execute(float delta, DataStore store, int entity, in T component)
        {
            _owner.OnRemoved(in component, entity);
            store.ClearRemoved<T>(entity);
        }
    }
}
