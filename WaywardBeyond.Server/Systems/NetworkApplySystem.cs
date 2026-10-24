using Swordfish.ECS;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Drains inbound client-owned snapshots (the replication apply stage). Splitting the replication
/// system's apply and publish stages into two ordered systems lets the world tick consume physics and
/// the shared motion step between them, which a single system position cannot express.
/// </summary>
public sealed class NetworkApplySystem(NetworkReplicationSystem replication) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        replication.ApplyStage(delta, store);
    }
}