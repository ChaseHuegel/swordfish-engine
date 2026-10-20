using System;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Config;
using WaywardBeyond.Networking;

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

    private int _nextHeartbeatTicks;

    public PendingConnectionHeartbeat(
        ServerPeers peers,
        NetworkingConfig config,
        ILogger<PendingConnectionHeartbeat> logger
    ) {
        _peers = peers;
        _logger = logger;
        _heartbeatIntervalMs = ServerHeartbeatService.ClampHeartbeat(config);
        _tpsWindowStartedTicks = Environment.TickCount;
        _nextHeartbeatTicks = Environment.TickCount;
    }

    /// <summary>Runs once per server update.</summary>
    public void Tick(uint playerCount)
    {
        MeasureTps();

        if (Environment.TickCount - _nextHeartbeatTicks < _heartbeatIntervalMs)
        {
            return;
        }

        _nextHeartbeatTicks = Environment.TickCount;
        var outbound = new ServerHeartbeatMessage
        {
            TPS = (uint)Math.Max(1, _tps),
            TickNumber = 0,
            PlayerCount = playerCount,
        };

        foreach (ServerPeer peer in _peers.Peers)
        {
            if (peer.World == null)
            {
                peer.Client.ServerHeartbeat(outbound);
            }
        }
    }

    private void MeasureTps()
    {
        _tpsFrames++;

        long now = Environment.TickCount;
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
