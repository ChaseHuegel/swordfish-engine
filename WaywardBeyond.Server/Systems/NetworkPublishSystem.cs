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
    private readonly NetworkReplicationSystem _replication = replication;
    private readonly SimulationStep _simulationStep = simulationStep;

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        _replication.SimTick = _simulationStep.CurrentSimTick;
        _replication.PublishStage(delta, store);
    }
}