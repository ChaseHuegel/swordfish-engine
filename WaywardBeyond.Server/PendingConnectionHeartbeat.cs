using System;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Server;

/// <summary>
/// Session heartbeats for peers that are connected but not bound to a world - the menu,
/// character-creation, and save-select window. The per-world <see cref="ServerHeartbeatService"/>
/// only reaches bound peers, so the host pulse keeps menu peers alive. Client heartbeats stamp the
/// peer's last inbound time in <see cref="ServerHandlers"/>.
/// </summary>
public sealed class PendingConnectionHeartbeat
{
    private readonly ServerPeers _peers;
    private readonly ILogger<PendingConnectionHeartbeat> _logger;
    private readonly int _heartbeatIntervalMs;

    private int _tpsFrames;
    private long _tpsWindowStartedTicks;
    private int _tps;

    private long _nextHeartbeatTicks;

    /// <summary>Creates the host pulse with its cadence clamped below the transport idle timeout.</summary>
    public PendingConnectionHeartbeat(
        ServerPeers peers,
        NetworkingConfig config,
        ILogger<PendingConnectionHeartbeat> logger
    ) {
        _peers = peers;
        _logger = logger;
        _heartbeatIntervalMs = ServerHeartbeatService.ClampHeartbeat(config);
        _tpsWindowStartedTicks = Environment.TickCount64;
        _nextHeartbeatTicks = Environment.TickCount64;
    }

    /// <summary>Runs once per server update.</summary>
    public void Tick(uint playerCount)
    {
        MeasureTps();

        if (Environment.TickCount64 - _nextHeartbeatTicks < _heartbeatIntervalMs)
        {
            return;
        }

        _nextHeartbeatTicks = Environment.TickCount64;
        var outbound = new ServerHeartbeatMessage
        {
            TPS = (uint)Math.Max(1, _tps),
            TickNumber = 0,
            PlayerCount = playerCount,
        };

        foreach (ServerPeer peer in _peers.Peers)
        {
            if (peer.World == null && !peer.Rpc.Notify(GameRpc.Methods.SERVER_HEARTBEAT, in outbound))
            {
                peer.CloseOnRejectedSend(_logger);
            }
        }
    }

    private void MeasureTps()
    {
        _tpsFrames++;

        long now = Environment.TickCount64;
        long elapsed = now - _tpsWindowStartedTicks;
        if (elapsed < 1000)
        {
            return;
        }

        _tps = (int)Math.Round(_tpsFrames * 1000d / elapsed);
        _tpsFrames = 0;
        _tpsWindowStartedTicks = now;
    }
}
