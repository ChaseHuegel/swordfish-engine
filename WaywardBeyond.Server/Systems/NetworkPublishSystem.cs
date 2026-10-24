using Swordfish.ECS;
using WaywardBeyond.Gameplay;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Publishes authoritative server-owned snapshots (the replication publish stage), the final stage of
/// the world tick. Runs after physics and interaction so the published tick number is the advanced sim
/// tick of the step that just applied.
/// </summary>
public sealed class NetworkPublishSystem(
    NetworkReplicationSystem replication,
    SimulationStep simulationStep
) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        replication.SimTick = simulationStep.CurrentSimTick;
        replication.PublishStage(delta, store);
    }
}