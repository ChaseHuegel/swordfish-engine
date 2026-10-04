using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Server.Core;

/// <summary>
/// Session heartbeats, server side. Emits <see cref="ServerHeartbeatMessage"/> per connected client at
/// <c>NetworkingSettings.HeartbeatIntervalMs</c> cadence (clamped below the connection timeout so the
/// heartbeat doubles as the transport keepalive - a live link always delivers a readable message within
/// the timeout window). Consumes <see cref="ClientHeartbeatMessage"/>s, tracking each client's reported
/// sim tick and logging a warn when it falls behind the server by more than
/// <c>NetworkingSettings.TickLagWarnThreshold</c> (once per crossing).
/// </summary>
public sealed class ServerHeartbeatService
{
    private readonly ServerConnectionHub _hub;
    private readonly ILogger<ServerHeartbeatService> _logger;
    private readonly int _heartbeatIntervalMs;
    private readonly uint _lagWarnThreshold;

    private readonly Dictionary<Uuid, uint> _lastClientTick = [];
    private readonly HashSet<Uuid> _lagWarned = [];

    private int _tpsFrames;
    private long _tpsWindowStartedTicks;
    private int _tps;

    private int _nextHeartbeatTicks;

    public ServerHeartbeatService(
        in ServerConnectionHub hub,
        in NetworkingSettings settings,
        in ILogger<ServerHeartbeatService> logger
    ) {
        _hub = hub;
        _logger = logger;
        _heartbeatIntervalMs = Math.Max(250, Math.Min(settings.HeartbeatIntervalMs.Get(), Math.Max(1, settings.ConnectionTimeoutMs.Get() / 2)));
        _lagWarnThreshold = (uint)Math.Max(1, settings.TickLagWarnThreshold.Get());
        _tpsWindowStartedTicks = Environment.TickCount;
        _nextHeartbeatTicks = Environment.TickCount;
    }

    /// <summary>Run once per server update.</summary>
    public void Pump(float delta, DataStore store, uint simTick)
    {
        MeasureTps();

        foreach ((Uuid clientId, ClientHeartbeatMessage heartbeat) in _hub.Receive<ClientHeartbeatMessage>())
        {
            TrackClientTick(clientId, heartbeat.TickNumber, simTick);
        }

        if (Environment.TickCount - _nextHeartbeatTicks < _heartbeatIntervalMs)
        {
            return;
        }

        _nextHeartbeatTicks = Environment.TickCount;
        var outbound = new ServerHeartbeatMessage
        {
            TPS = (uint)Math.Max(1, _tps),
            TickNumber = simTick,
            PlayerCount = (uint)_hub.Count,
        };

        foreach ((Uuid clientId, _) in _hub.Clients)
        {
            _hub.Send(clientId, outbound);
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

    private void TrackClientTick(Uuid clientId, uint clientTick, uint serverTick)
    {
        _lastClientTick[clientId] = clientTick;

        if (serverTick <= clientTick || serverTick - clientTick <= _lagWarnThreshold)
        {
            _lagWarned.Remove(clientId);
            return;
        }

        if (_lagWarned.Add(clientId))
        {
            _logger.LogWarning("Client {client} sim tick {clientTick} is {lag} ticks behind the server ({serverTick}).", clientId, clientTick, serverTick - clientTick, serverTick);
        }
    }
}