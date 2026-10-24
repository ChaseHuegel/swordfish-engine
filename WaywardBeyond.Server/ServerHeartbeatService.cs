using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;

namespace WaywardBeyond.Server;

/// <summary>
/// Session heartbeats, server side. Sends <see cref="ServerHeartbeatMessage"/> to every peer in the
/// world at <c>NetworkingConfig.Protocol.HeartbeatIntervalMs</c> cadence, clamped below the idle
/// timeout. Consumes <see cref="ClientHeartbeatMessage"/>s and logs a warn when a client's sim tick
/// falls behind by more than <c>NetworkingConfig.Server.TickLagWarnThreshold</c>.
/// </summary>
public sealed class ServerHeartbeatService(
    ServerPeers peers,
    SessionManager sessions,
    NetworkingConfig config,
    ILogger<ServerHeartbeatService> logger,
    SimulationStep simulationStep
) : IServerWorldSystem
{
    private readonly int _heartbeatIntervalMs = ClampHeartbeat(config);
    private readonly uint _lagWarnThreshold = (uint)Math.Max(1, config.Server.TickLagWarnThreshold.Get());

    private readonly HashSet<Uuid> _lagWarned = [];
    private readonly List<Uuid> _lagWarnedSnapshot = [];

    private int _tps;
    private int _tpsFrames;
    private long _tpsWindowStartedTicks = Environment.TickCount64;
    private long _nextHeartbeatTicks = Environment.TickCount64;

    public void Tick(float delta, DataStore store)
    {
        MeasureTps();
        uint simTick = simulationStep.CurrentSimTick;

        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            while (peer.Heartbeats.TryDequeue(out ClientHeartbeatMessage heartbeat))
            {
                TrackClientTick(clientId, heartbeat.TickNumber, simTick);
            }
        }

        //  A lag-warned client that disconnected stops sending heartbeats, so TrackClientTick can no
        //  longer retire the entry; prune it here instead so the set cannot grow with client churn.
        if (_lagWarned.Count > 0)
        {
            _lagWarnedSnapshot.Clear();
            _lagWarnedSnapshot.AddRange(_lagWarned);
            for (var index = 0; index < _lagWarnedSnapshot.Count; index++)
            {
                if (!peers.TryGet(_lagWarnedSnapshot[index], out _))
                {
                    _lagWarned.Remove(_lagWarnedSnapshot[index]);
                }
            }
        }

        if (Environment.TickCount64 - _nextHeartbeatTicks < _heartbeatIntervalMs)
        {
            return;
        }

        _nextHeartbeatTicks = Environment.TickCount64;
        var outbound = new ServerHeartbeatMessage
        {
            TPS = (uint)Math.Max(1, _tps),
            TickNumber = simTick,
            PlayerCount = (uint)sessions.Count,
        };

        foreach (Uuid clientId in sessions.Clients)
        {
            if (peers.TryGet(clientId, out ServerPeer peer))
            {
                peer.Client.ServerHeartbeat(outbound);
            }
        }
    }

    /// <summary>Clamps the heartbeat below the transport idle timeout so it stays a keepalive.</summary>
    internal static int ClampHeartbeat(NetworkingConfig config)
    {
        int requested = config.Protocol.HeartbeatIntervalMs.Get();
        int idle = config.Transport.IdleTimeoutMs.Get();
        return idle > 0 ? Math.Max(250, Math.Min(requested, idle / 2)) : Math.Max(250, requested);
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

    private void TrackClientTick(Uuid clientId, uint clientTick, uint serverTick)
    {
        if (serverTick <= clientTick || serverTick - clientTick <= _lagWarnThreshold)
        {
            _lagWarned.Remove(clientId);
            return;
        }

        if (_lagWarned.Add(clientId))
        {
            logger.LogWarning("Client {client} sim tick {clientTick} is {lag} ticks behind the server ({serverTick}).", clientId, clientTick, serverTick - clientTick, serverTick);
        }
    }
}
