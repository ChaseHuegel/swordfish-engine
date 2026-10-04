using System;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core;

/// <summary>
/// Session heartbeats, client side. Emits <see cref="ClientHeartbeatMessage"/> at
/// <c>NetworkingSettings.HeartbeatIntervalMs</c> cadence (clamped below the connection timeout) from
/// connection establishment - not gated on join - so the heartbeat doubles as the transport keepalive.
/// Consumes <see cref="ServerHeartbeatMessage"/>s into <see cref="ServerStats"/> for the F3 screen.
/// </summary>
internal sealed class ClientHeartbeatSystem : IEntitySystem
{
    private readonly TransportManager _transport;
    private readonly SnapshotAckTracker _snapshotAck;
    private readonly ClientPlayerMotionProcessor _motionProcessor;
    private readonly NetworkingSettings _settings;
    private readonly ServerStats _stats;
    private readonly ILogger<ClientHeartbeatSystem> _logger;

    private int _nextHeartbeatTicks;

    public ClientHeartbeatSystem(
        in TransportManager transport,
        in SnapshotAckTracker snapshotAck,
        in ClientPlayerMotionProcessor motionProcessor,
        in NetworkingSettings settings,
        in ServerStats stats,
        in ILogger<ClientHeartbeatSystem> logger
    ) {
        _transport = transport;
        _snapshotAck = snapshotAck;
        _motionProcessor = motionProcessor;
        _settings = settings;
        _stats = stats;
        _logger = logger;
        _nextHeartbeatTicks = Environment.TickCount;
    }

    public void Tick(float delta, DataStore store)
    {
        IClientConnection? active = _transport.Active;
        if (active == null)
        {
            return;
        }

        while (active.Receive<ServerHeartbeatMessage>() is { Success: true } heartbeat)
        {
            _stats.Record(heartbeat.Value);
        }

        int intervalMs = Math.Max(250, Math.Min(_settings.HeartbeatIntervalMs.Get(), Math.Max(1, _settings.ConnectionTimeoutMs.Get() / 2)));
        if (Environment.TickCount - _nextHeartbeatTicks < intervalMs)
        {
            return;
        }

        _nextHeartbeatTicks = Environment.TickCount;
        Result send = active.Send(new ClientHeartbeatMessage
        {
            TickNumber = _motionProcessor.Step?.CurrentSimTick ?? 0,
            LastAppliedSnapshotTick = _snapshotAck.LastAppliedSnapshotTick,
        });
        if (!send.Success)
        {
            _logger.LogDebug("Failed to send session heartbeat: {message}.", send.Message);
        }
    }
}