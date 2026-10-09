using Swordfish.ECS;
using WaywardBeyond.Server;
using WaywardBeyond.Gameplay;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Publishes authoritative server-owned snapshots (the replication publish stage), the final stage of
/// the world tick. Runs after physics and interaction so the published tick number is the advanced sim
/// tick of the step that just applied.
/// </summary>
public sealed class NetworkPublishSystem : IServerWorldSystem
{
    private readonly NetworkReplicationSystem _replication;
    private readonly SharedSimulationStep _simulationStep;

    public NetworkPublishSystem(
        in NetworkReplicationSystem replication,
        in SharedSimulationStep simulationStep
    ) {
        _replication = replication;
        _simulationStep = simulationStep;
    }

    public void Tick(float delta, DataStore store)
    {
        _replication.SimTick = _simulationStep.CurrentSimTick;
        _replication.PublishStage(delta, store);
    }
}