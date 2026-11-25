using Krill;

namespace Swordfish.ECS;

/// <summary>Ticks every registered component resource processor and disposes removed disposable components.</summary>
public sealed class ResourceLifetimeSystem(in IComponentResourceProcessor[] processors) : IEntitySystem
{
    private readonly IComponentResourceProcessor[] _processors = processors;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        for (var i = 0; i < _processors.Length; i++)
        {
            _processors[i].Process(store, delta);
        }

        store.DisposeRemoved();
    }
}
