using System;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Session heartbeats for connections accepted but not yet bound to a world hub - the menu and
/// character-creation window. The per-world <see cref="ServerHeartbeatService"/> only reaches hub
/// clients, so a pre-join connection would otherwise receive nothing and time out its read socket.
/// This pump emits <see cref="ServerHeartbeatMessage"/> per pending connection at
/// <c>NetworkingSettings.HeartbeatIntervalMs</c> cadence (clamped below the connection timeout) from
/// connection establishment - not gated on join - and drains the <see cref="ClientHeartbeatMessage"/>s
/// they send so no world's lag tracking has to.
/// </summary>
public sealed class ServerHostHeartbeat
{
    private readonly PendingJoins _pendingJoins;
    private readonly ILogger<ServerHostHeartbeat> _logger;
    private readonly int _heartbeatIntervalMs;

    private int _tpsFrames;
    private long _tpsWindowStartedTicks;
    private int _tps;

    private int _nextHeartbeatTicks;

    public ServerHostHeartbeat(
        in PendingJoins pendingJoins,
        in NetworkingSettings settings,
        in ILogger<ServerHostHeartbeat> logger
    ) {
        _pendingJoins = pendingJoins;
        _logger = logger;
        _heartbeatIntervalMs = Math.Max(250, Math.Min(settings.HeartbeatIntervalMs.Get(), Math.Max(1, settings.ConnectionTimeoutMs.Get() / 2)));
        _tpsWindowStartedTicks = Environment.TickCount;
        _nextHeartbeatTicks = Environment.TickCount;
    }

    /// <summary>Runs once per server update.</summary>
    public void Tick(uint playerCount)
    {
        MeasureTps();

        foreach (IServerConnection connection in _pendingJoins.Snapshot())
        {
            //  No world tracks a pending connection's lag yet; drain its heartbeats rather than let them
            //  accumulate in the transport's unbounded per-type receive queue.
            while (connection.Receive<ClientHeartbeatMessage>().Success)
            {
            }
        }

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

        foreach (IServerConnection connection in _pendingJoins.Snapshot())
        {
            Result send = connection.Send(outbound);
            if (!send.Success)
            {
                _logger.LogDebug("Failed to send pending session heartbeat: {message}.", send.Message);
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
