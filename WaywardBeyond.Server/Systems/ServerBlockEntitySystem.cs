using Krill;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world system that detaches block-entity viewers that move out of reach or lose access before
/// snapshots publish.
/// </summary>
public sealed class ServerBlockEntitySystem(ServerBlockEntityService blockEntities) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        blockEntities.DetachOutOfReachViewers(store);
    }
}
