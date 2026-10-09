using Swordfish.ECS;
using WaywardBeyond.Server;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Drains inbound client-owned snapshots (the replication apply stage). Splitting the replication
/// system's apply and publish stages into two ordered systems lets the world tick consume physics and
/// the shared motion step between them, which a single system position cannot express.
/// </summary>
public sealed class NetworkApplySystem : IServerWorldSystem
{
    private readonly NetworkReplicationSystem _replication;

    public NetworkApplySystem(in NetworkReplicationSystem replication)
    {
        _replication = replication;
    }

    public void Tick(float delta, DataStore store)
    {
        _replication.ApplyStage(delta, store);
    }
}