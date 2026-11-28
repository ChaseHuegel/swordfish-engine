using System.Collections.Generic;
using Krill;
using WaywardBeyond.Server.Saves;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Per-world system that detaches brick-entity viewers that move out of reach or lose access and
/// sweeps emptied auto-despawn containers before snapshots publish.
/// </summary>
public sealed class ServerBrickEntitySystem(
    ServerBrickEntityService brickEntities,
    LevelSaveService? saveService = null
) : IServerWorldSystem
{
    private readonly List<Uuid> _despawnedStructures = [];

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        brickEntities.DetachOutOfReachViewers(store);

        _despawnedStructures.Clear();
        brickEntities.SweepEmptyAutoDespawnContainers(store, _despawnedStructures);
        for (var i = 0; i < _despawnedStructures.Count; i++)
        {
            saveService?.QueueDeleteStructure(_despawnedStructures[i]);
        }

        _despawnedStructures.Clear();
    }
}
