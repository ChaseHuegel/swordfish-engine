using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Config;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Transport;

namespace WaywardBeyond.Server;

/// <summary>
/// Session heartbeats, server side. Emits <see cref="ServerHeartbeatMessage"/> per connected client at
/// <c>NetworkingConfig.Protocol.HeartbeatIntervalMs</c> cadence (clamped below the connection timeout so the
/// heartbeat doubles as the transport keepalive - a live link always delivers a readable message within
/// the timeout window). Consumes <see cref="ClientHeartbeatMessage"/>s, tracking each client's reported
/// sim tick and logging a warn when it falls behind the server by more than
/// <c>NetworkingConfig.Server.TickLagWarnThreshold</c> (once per crossing).
/// </summary>
public sealed class ServerHeartbeatService : IServerWorldSystem
{
    private readonly ServerConnectionHub _hub;
    private readonly ILogger<ServerHeartbeatService> _logger;
    private readonly SimulationStep _simulationStep;
    private readonly int _heartbeatIntervalMs;
    private readonly uint _lagWarnThreshold;

    private readonly HashSet<Uuid> _lagWarned = [];

    private int _tpsFrames;
    private long _tpsWindowStartedTicks;
    private int _tps;

    private int _nextHeartbeatTicks;

    public ServerHeartbeatService(
        ServerConnectionHub hub,
        NetworkingConfig config,
        ILogger<ServerHeartbeatService> logger,
        SimulationStep simulationStep
    ) {
        _hub = hub;
        _logger = logger;
        _simulationStep = simulationStep;
        _heartbeatIntervalMs = Math.Max(250, Math.Min(config.Protocol.HeartbeatIntervalMs.Get(), Math.Max(1, config.Transport.TimeoutMs.Get() / 2)));
        _lagWarnThreshold = (uint)Math.Max(1, config.Server.TickLagWarnThreshold.Get());
        _tpsWindowStartedTicks = Environment.TickCount;
        _nextHeartbeatTicks = Environment.TickCount;
    }

    public void Tick(float delta, DataStore store)
    {
        MeasureTps();
        uint simTick = _simulationStep.CurrentSimTick;

        foreach ((Uuid clientId, ClientHeartbeatMessage heartbeat) in _hub.Receive<ClientHeartbeatMessage>())
        {
            TrackClientTick(clientId, heartbeat.TickNumber, simTick);
        }

        //  A lag-warned client that disconnected stops sending heartbeats, so TrackClientTick can no
        //  longer retire the entry; prune it here instead so the set cannot grow with client churn.
        foreach (Uuid clientId in _lagWarned.ToArray())
        {
            if (!_hub.TryGet(clientId, out _))
            {
                _lagWarned.Remove(clientId);
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