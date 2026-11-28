using Krill;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world system that reconciles open station bindings and detaches brick-entity viewers that move
/// out of reach or lose access before snapshots publish.
/// </summary>
public sealed class ServerBrickEntitySystem(
    ServerBrickEntityService brickEntities,
    ServerStationService? stations = null
) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        stations?.ReconcileActiveStations(store);
        brickEntities.DetachOutOfReachViewers(store);
    }
}
