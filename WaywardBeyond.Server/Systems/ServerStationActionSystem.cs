using Krill;
using Microsoft.Extensions.Logging;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Gameplay.Stations;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server-side consumption of staged <see cref="StationActionOp"/> commands. Drains each player's
/// sequence-deduped <see cref="NetworkComponent.StagedStationActions"/> ring in ascending order and
/// dispatches each command through <see cref="IStationActionHandlerRegistry"/>.
/// </summary>
public sealed class ServerStationActionSystem(
    SessionManager sessions,
    IStationActionHandlerRegistry handlerRegistry,
    ServerStationService stations,
    SimulationStep simulationStep,
    ILogger<ServerStationActionSystem> logger
) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        ConsumeActionsAction action = new() { Owner = this };
        store.QueryRef<NetworkComponent, OwnedCharacterComponent, ConsumeActionsAction>(delta, ref action);
    }

    private uint Consume(DataStore store, int entity, in NetworkComponent net)
    {
        uint consumedSequence = net.LastConsumedStationActionSequence;
        StageRing<StagedStationAction>? staged = net.StagedStationActions;
        if (staged == null || staged.Count == 0)
        {
            return consumedSequence;
        }

        Result<StagedStationAction> next = staged.Consume(consumedSequence);
        if (!next.Success)
        {
            staged.Clear();
            return consumedSequence;
        }

        Uuid playerUuid = store.GetUuid(entity);
        Uuid clientId = sessions.TryGetClient(entity, out Uuid resolvedClient) ? resolvedClient : Uuid.Null;
        GameMode mode = store.TryGet(entity, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Adventure;
        long simTick = simulationStep.CurrentSimTick;
        float fixedDelta = simulationStep.FixedDelta;

        while (next.Success)
        {
            StagedStationAction entry = next.Value;
            consumedSequence = entry.SequenceNumber;

            //  An invalid or rejected op is a no-op, but its sequence is still consumed so it cannot replay.
            DispatchAction(store, entity, playerUuid, clientId, entry.SequenceNumber, in entry.Op, mode, simTick, fixedDelta);
            next = staged.Consume(consumedSequence);
        }

        staged.Clear();
        return consumedSequence;
    }

    private void DispatchAction(
        DataStore store,
        int playerEntity,
        Uuid playerUuid,
        Uuid clientId,
        uint sequenceNumber,
        in StationActionOp op,
        GameMode mode,
        long simTick,
        float fixedDelta
    )
    {
        if (!op.IsWithinWireBounds())
        {
            logger.LogDebug(
                "Rejected station action {sequence} for player {player}: invalid kind or wire bounds.",
                sequenceNumber, playerEntity);
            return;
        }

        var context = new StationActionContext(
            store,
            playerEntity,
            playerUuid,
            clientId,
            sequenceNumber,
            in op,
            mode,
            simTick,
            fixedDelta,
            stations);

        StationActionResult result = handlerRegistry.Dispatch(in context);
        if (result.Decision != StationActionDecision.Handled)
        {
            logger.LogDebug(
                "Station action {sequence} ({action}) for player {player} ended with {decision}: {reason}",
                sequenceNumber, op.Action, playerEntity, result.Decision, result.Reason);
        }
    }

    private struct ConsumeActionsAction : IForEachRef<NetworkComponent, OwnedCharacterComponent>
    {
        public ServerStationActionSystem Owner;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net, ref Ref<OwnedCharacterComponent> owned)
        {
            ref readonly NetworkComponent current = ref net.Read;
            uint consumedSequence = Owner.Consume(store, entity, in current);

            //  Change-gated: a write of an unchanged value still marks the component dirty.
            if (consumedSequence != current.LastConsumedStationActionSequence)
            {
                net.Write.LastConsumedStationActionSequence = consumedSequence;
            }
        }
    }
}
