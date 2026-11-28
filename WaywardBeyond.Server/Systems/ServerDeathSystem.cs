using System.Collections.Generic;
using Krill;
using NsdRpc;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server world system that drains inbound respawn RPC requests and transitions any zero-health
/// Adventure Mode player into the authoritative dead state. Runs directly after
/// <see cref="ServerSurvivalSystem"/> and before interaction and tool systems so fatal damage from
/// physics or survival takes effect on the same tick.
/// </summary>
public sealed class ServerDeathSystem(
    ServerDeathService deathService,
    ServerPeers peers,
    SessionManager sessions,
    SimulationStep simulationStep
) : IServerWorldSystem
{
    private const string NOT_DEAD_ERROR_KEY = "notification.respawn.notDead";

    private readonly List<int> _dyingPlayers = [];

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        DrainRespawnCalls(store);
        ProcessNewlyDeadPlayers(store);
    }

    private void DrainRespawnCalls(DataStore store)
    {
        foreach (Uuid clientId in sessions.Clients)
        {
            if (!peers.TryGet(clientId, out ServerPeer peer))
            {
                continue;
            }

            while (peer.RespawnCalls.TryDequeue(out ServerPeer.RespawnCall call))
            {
                HandleRespawnCall(store, peer, in call);
            }
        }
    }

    private void HandleRespawnCall(DataStore store, ServerPeer peer, in ServerPeer.RespawnCall call)
    {
        if (call.CancellationToken.IsCancellationRequested)
        {
            call.Completion.TrySetResult(RpcResult<RespawnResponse>.Failure(
                GameRpcErrors.INVALID_STATE, "the respawn request was cancelled"));
            return;
        }

        if (!sessions.TryGetEntity(peer.GameId, out int playerEntity))
        {
            call.Completion.TrySetResult(RpcResult<RespawnResponse>.Failure(
                GameRpcErrors.NOT_JOINED, "the peer has no active session entity"));
            return;
        }

        Result respawn = deathService.RespawnPlayer(store, playerEntity);
        call.Completion.TrySetResult(RpcResult<RespawnResponse>.Success(new RespawnResponse
        {
            Success = respawn.Success,
            ErrorKey = respawn.Success ? null : NOT_DEAD_ERROR_KEY,
        }));
    }

    private void ProcessNewlyDeadPlayers(DataStore store)
    {
        _dyingPlayers.Clear();
        CollectDyingAction action = new() { DyingPlayers = _dyingPlayers };
        store.Query<StatisticsComponent, GameModeComponent, OwnedCharacterComponent, CollectDyingAction>(0f, ref action);

        long simTick = simulationStep.CurrentSimTick;
        for (var i = 0; i < _dyingPlayers.Count; i++)
        {
            deathService.HandleDeathIfNeeded(store, _dyingPlayers[i], simTick);
        }

        _dyingPlayers.Clear();
    }

    private struct CollectDyingAction : IForEach<StatisticsComponent, GameModeComponent, OwnedCharacterComponent>
    {
        public List<int> DyingPlayers;

        public void Execute(
            float delta,
            DataStore store,
            int entity,
            in StatisticsComponent stats,
            in GameModeComponent mode,
            in OwnedCharacterComponent owned
        )
        {
            if (mode.Mode == GameMode.Creative || stats.IsDead)
            {
                return;
            }

            if (stats.TryGetValue(StatisticIds.HEALTH, out long rawHealth) && rawHealth <= 0L)
            {
                DyingPlayers.Add(entity);
            }
        }
    }
}
