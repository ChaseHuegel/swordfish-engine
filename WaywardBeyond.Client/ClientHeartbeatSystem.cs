using System;
using Swordfish.ECS;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Client;

/// <summary>
/// Session heartbeats, client side. Sends <see cref="ClientHeartbeatMessage"/> at
/// <c>NetworkingConfig.Protocol.HeartbeatIntervalMs</c> cadence, clamped below the idle timeout, so the
/// heartbeat doubles as the transport keepalive. Drains <see cref="ServerHeartbeatMessage"/>s into
/// <see cref="ServerStats"/> for the F3 screen.
/// </summary>
internal sealed class ClientHeartbeatSystem : IEntitySystem
{
    private readonly ClientRpcHost _host;
    private readonly ClientInbound _inbound;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly int _heartbeatIntervalMs;
    private readonly ServerStats _stats;

    private int _nextHeartbeatTicks;

    public ClientHeartbeatSystem(
        in ClientRpcHost host,
        in ClientInbound inbound,
        in SnapshotAckTracker snapshotAck,
        in ClientPlayerMotionProcessor motionProcessor,
        in NetworkingConfig config,
        in ServerStats stats
    ) {
        _host = host;
        _inbound = inbound;
        _snapshotAck = snapshotAck;
        _motionProcessor = motionProcessor;
        _stats = stats;
        _heartbeatIntervalMs = ServerHeartbeatInterval(config);
        _nextHeartbeatTicks = Environment.TickCount;
    }

    public void Tick(float delta, DataStore store)
    {
        while (_inbound.Heartbeats.TryDequeue(out ServerHeartbeatMessage heartbeat))
        {
            _stats.Record(heartbeat);
        }

        if (Environment.TickCount - _nextHeartbeatTicks < _heartbeatIntervalMs)
        {
            return;
        }

        _nextHeartbeatTicks = Environment.TickCount;
        _host.Api?.ClientHeartbeat(new ClientHeartbeatMessage
        {
            TickNumber = _motionProcessor.Step?.CurrentSimTick ?? 0,
            LastAppliedSnapshotTick = _snapshotAck.LastAppliedSnapshotTick,
        });
    }

    private static int ServerHeartbeatInterval(NetworkingConfig config)
    {
        int requested = config.Protocol.HeartbeatIntervalMs.Get();
        int idle = config.Transport.IdleTimeoutMs.Get();
        return idle > 0 ? Math.Max(250, Math.Min(requested, idle / 2)) : Math.Max(250, requested);
    }
}
