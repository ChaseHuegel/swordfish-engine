using Krill;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world system that detaches brick-entity viewers that move out of reach or lose access before
/// snapshots publish.
/// </summary>
public sealed class ServerBrickEntitySystem(ServerBrickEntityService brickEntities) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        brickEntities.DetachOutOfReachViewers(store);
    }
}
