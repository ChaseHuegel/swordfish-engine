using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Server.Components;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// The authoritative interaction pump. Runs once per server tick, between the replication apply stage
/// (which drains inbound client interaction events into each player mirror's staged buffer) and the
/// publish stage. Immediate interactions commit on the same tick; any interaction whose validation
/// stage sets <see cref="InteractionContext.Hold"/> advances a server-validated timed session until
/// completion or cancellation.
/// </summary>
public sealed class ServerInteractionSystem(
    SessionManager sessions,
    ILogger<ServerInteractionSystem> logger,
    IInteractionHandlerRegistry handlerRegistry,
    Func<DataStore, IVoxelInteractionWorld> worldFactory,
    SimulationStep simulationStep,
    ServerBrickEntityService? brickEntities = null
) : IServerWorldSystem
{
    private readonly Dictionary<Uuid, TimedSession> _timedSessions = [];
    private readonly HashSet<Uuid> _visitedPlayers = [];
    private readonly List<Uuid> _stalePlayers = [];
    private DataStore? _cachedWorldStore;
    private IVoxelInteractionWorld? _cachedWorld;

    /// <summary>Gets the active timed interaction session for a player mirror, if one is running.</summary>
    internal Result<TimedSessionState> GetTimedSession(Uuid playerUuid)
    {
        if (_timedSessions.TryGetValue(playerUuid, out TimedSession session))
        {
            return Result<TimedSessionState>.FromSuccess(new TimedSessionState(
                session.Key,
                session.ActiveSlot,
                session.HeldItemId,
                session.Elapsed,
                session.RequiredDuration));
        }

        return Result<TimedSessionState>.FromFailure("No active timed interaction session.");
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        Tick(delta, store, simulationStep.CurrentSimTick);
    }

    /// <summary>Processes staged interactions and advances active timed sessions for <paramref name="simTick"/>.</summary>
    public void Tick(float delta, DataStore store, uint simTick)
    {
        float validDelta = float.IsFinite(delta) && delta > 0f ? delta : 0f;
        _visitedPlayers.Clear();

        ConsumeAction action = new() { Owner = this, Store = store, SimTick = simTick };
        store.QueryRef<NetworkComponent, OwnedCharacterComponent, TransformComponent, ConsumeAction>(validDelta, ref action);

        PruneStaleSessions();
    }

    private void ProcessPlayer(
        DataStore store,
        int entity,
        float delta,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform,
        out uint consumedSequence
    ) {
        Uuid playerUuid = store.GetUuid(entity);
        _visitedPlayers.Add(playerUuid);

        consumedSequence = net.LastConsumedInteractionSequence;
        if (net.StagedInteractions != null)
        {
            StageRing<InteractionEvent> buffer = net.StagedInteractions;
            Result<InteractionEvent> staged = buffer.Consume(simTick, consumedSequence);
            while (staged.Success)
            {
                InteractionEvent interaction = staged.Value;
                consumedSequence = interaction.SequenceNumber;
                ProcessInteraction(store, entity, playerUuid, simTick, in net, in interaction, in transform);
                staged = buffer.Consume(simTick, consumedSequence);
            }
        }

        AdvanceTimedSession(store, entity, playerUuid, delta, simTick, in net, in transform);
    }

    private void ProcessInteraction(
        DataStore store,
        int player,
        Uuid playerUuid,
        uint simTick,
        in NetworkComponent net,
        in InteractionEvent interaction,
        in TransformComponent mirror
    ) {
        var kind = (InteractionKind)interaction.Kind;
        GameMode mode = GetGameMode(store, player);

        if (ShouldCancelOnRelease(playerUuid, kind))
        {
            _timedSessions.Remove(playerUuid);
        }

        TryGetHeldEquipment(store, player, out int activeSlot, out string? heldItemId);
        IVoxelInteractionWorld world = GetWorld(store);
        Uuid clientId = sessions.TryGetClient(player, out Uuid client) ? client : Uuid.Null;
        InteractionContext context = BuildContext(
            store,
            player,
            clientId,
            interaction.SequenceNumber,
            world,
            in mirror,
            interaction.Brick,
            interaction.Paint,
            kind,
            mode,
            heldItemId,
            simTick,
            simulationStep.FixedDelta);

        InteractionRunResult preCheck = handlerRegistry.Run(ref context, InteractionStage.Select, InteractionStage.Validate);
        if (preCheck.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
        {
            CancelMatchingSession(playerUuid, kind);
            logger.LogDebug(
                "Interaction {sequence} for player {player} ended with {decision}.",
                interaction.SequenceNumber, player, preCheck.Decision);
            return;
        }

        if (context.Hold.IsRequired)
        {
            if (!IsInputHeld(store, player, in net, simTick, context.Hold.RequiredInput))
            {
                _timedSessions.Remove(playerUuid);
                return;
            }

            if (_timedSessions.TryGetValue(playerUuid, out TimedSession existing) &&
                MatchesSession(in existing, in context.Hold, activeSlot, heldItemId))
            {
                existing.Sequence = interaction.SequenceNumber;
                existing.BrickHint = interaction.Brick;
                existing.PaintHint = interaction.Paint;
                existing.Kind = kind;
                existing.RequiredDuration = context.Hold.RequiredDuration;
                existing.RequiredInput = context.Hold.RequiredInput;
                existing.CancelOnEquipmentChange = context.Hold.CancelOnEquipmentChange;
                _timedSessions[playerUuid] = existing;
                return;
            }

            _timedSessions[playerUuid] = new TimedSession
            {
                Key = context.Hold.Key,
                RequiredInput = context.Hold.RequiredInput,
                CancelOnEquipmentChange = context.Hold.CancelOnEquipmentChange,
                BrickHint = interaction.Brick,
                PaintHint = interaction.Paint,
                Kind = kind,
                ActiveSlot = activeSlot,
                HeldItemId = heldItemId,
                Sequence = interaction.SequenceNumber,
                Elapsed = 0f,
                RequiredDuration = context.Hold.RequiredDuration,
            };
            return;
        }

        CancelMatchingSession(playerUuid, kind);

        InteractionRunResult commitResult = handlerRegistry.Run(ref context, InteractionStage.Commit, InteractionStage.Commit);
        if (commitResult.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} ended with {decision}.",
                interaction.SequenceNumber, player, commitResult.Decision);
            return;
        }

        if (commitResult.Resolution.Action == InteractionAction.None)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} resolved to nothing.",
                interaction.SequenceNumber, player);
            return;
        }

        NotifyBrickEntityInteraction(store, in context);

        InteractionRunResult reactResult = handlerRegistry.Run(ref context, InteractionStage.React, InteractionStage.React);
        if (reactResult.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} ended in react stage with {decision}.",
                interaction.SequenceNumber, player, reactResult.Decision);
        }
    }

    private void AdvanceTimedSession(
        DataStore store,
        int entity,
        Uuid playerUuid,
        float delta,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform
    ) {
        if (!_timedSessions.TryGetValue(playerUuid, out TimedSession session))
        {
            return;
        }

        if (!IsInputHeld(store, entity, in net, simTick, session.RequiredInput))
        {
            _timedSessions.Remove(playerUuid);
            return;
        }

        TryGetHeldEquipment(store, entity, out int activeSlot, out string? heldItemId);
        if (session.CancelOnEquipmentChange &&
            (activeSlot != session.ActiveSlot || !string.Equals(heldItemId, session.HeldItemId, StringComparison.Ordinal)))
        {
            _timedSessions.Remove(playerUuid);
            return;
        }

        GameMode mode = GetGameMode(store, entity);
        IVoxelInteractionWorld world = GetWorld(store);
        Uuid clientId = sessions.TryGetClient(entity, out Uuid client) ? client : Uuid.Null;
        InteractionContext context = BuildContext(
            store,
            entity,
            clientId,
            session.Sequence,
            world,
            in transform,
            session.BrickHint,
            session.PaintHint,
            session.Kind,
            mode,
            heldItemId,
            simTick,
            simulationStep.FixedDelta);

        InteractionRunResult validation = handlerRegistry.Run(ref context, InteractionStage.Select, InteractionStage.Validate);
        if (validation.Decision is InteractionDecision.Consume or InteractionDecision.Cancel ||
            !context.Hold.IsRequired ||
            context.Hold.Key != session.Key)
        {
            _timedSessions.Remove(playerUuid);
            return;
        }

        float requiredDuration = context.Hold.RequiredDuration;
        float elapsed = session.Elapsed + delta;

        if (TimedInteractionProgress.IsComplete(elapsed, requiredDuration))
        {
            _timedSessions.Remove(playerUuid);
            InteractionRunResult commitResult = handlerRegistry.Run(ref context, InteractionStage.Commit, InteractionStage.Commit);
            if (commitResult.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
            {
                logger.LogDebug(
                    "Timed interaction {sequence} for player {player} ended with {decision}.",
                    session.Sequence, entity, commitResult.Decision);
                return;
            }

            if (commitResult.Resolution.Action != InteractionAction.None)
            {
                NotifyBrickEntityInteraction(store, in context);
            }

            InteractionRunResult reactResult = handlerRegistry.Run(ref context, InteractionStage.React, InteractionStage.React);
            if (reactResult.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
            {
                logger.LogDebug(
                    "Timed interaction {sequence} for player {player} ended in react stage with {decision}.",
                    session.Sequence, entity, reactResult.Decision);
            }

            return;
        }

        session.Elapsed = elapsed;
        session.RequiredDuration = requiredDuration;
        session.RequiredInput = context.Hold.RequiredInput;
        session.CancelOnEquipmentChange = context.Hold.CancelOnEquipmentChange;
        _timedSessions[playerUuid] = session;
    }

    private void NotifyBrickEntityInteraction(DataStore store, in InteractionContext context)
    {
        if (brickEntities == null)
        {
            return;
        }

        InteractionResolution resolution = context.Resolution;
        if (resolution.Action == InteractionAction.Place)
        {
            brickEntities.OnBrickPlaced(store, store.GetUuid(resolution.Entity), resolution.Coordinate, resolution.Voxel.ID);
        }
        else if (resolution.Action == InteractionAction.Break)
        {
            brickEntities.OnBrickBroken(store, store.GetUuid(resolution.Entity), resolution.Coordinate, context.Player, context.GameMode);
        }
    }

    private bool ShouldCancelOnRelease(Uuid playerUuid, InteractionKind kind)
    {
        if (!_timedSessions.TryGetValue(playerUuid, out TimedSession session))
        {
            return false;
        }

        return kind.IsRelease() && kind.ToInput() == session.RequiredInput;
    }

    private void CancelMatchingSession(Uuid playerUuid, InteractionKind kind)
    {
        if (!_timedSessions.TryGetValue(playerUuid, out TimedSession session))
        {
            return;
        }

        InteractionInput input = kind.ToInput();
        if (input != InteractionInput.None && input == session.RequiredInput)
        {
            _timedSessions.Remove(playerUuid);
        }
    }

    private bool IsInputHeld(
        DataStore store,
        int entity,
        in NetworkComponent net,
        uint simTick,
        InteractionInput requiredInput
    ) {
        if (requiredInput == InteractionInput.None)
        {
            return true;
        }

        InputComponent input;
        if (net.StagedInputs != null && net.StagedInputs.Count > 0)
        {
            Result<InputComponent> staged = net.StagedInputs.GetCommand(simTick, simulationStep.CommandMaxAgeTicks);
            if (!staged.Success)
            {
                return false;
            }

            input = staged.Value;
        }
        else if (!store.TryGet(entity, out input))
        {
            return false;
        }

        return requiredInput.IsHeld(in input);
    }

    private IVoxelInteractionWorld GetWorld(DataStore store)
    {
        if (!ReferenceEquals(_cachedWorldStore, store) || _cachedWorld == null)
        {
            _cachedWorldStore = store;
            _cachedWorld = worldFactory(store);
        }

        return _cachedWorld;
    }

    private void PruneStaleSessions()
    {
        if (_timedSessions.Count == 0)
        {
            return;
        }

        _stalePlayers.Clear();
        foreach (Uuid playerUuid in _timedSessions.Keys)
        {
            if (!_visitedPlayers.Contains(playerUuid))
            {
                _stalePlayers.Add(playerUuid);
            }
        }

        for (var i = 0; i < _stalePlayers.Count; i++)
        {
            _timedSessions.Remove(_stalePlayers[i]);
        }
    }

    private static InteractionContext BuildContext(
        DataStore store,
        int player,
        Uuid clientId,
        uint sequence,
        IVoxelInteractionWorld world,
        in TransformComponent mirror,
        BrickInteraction? brickHint,
        PaintInteraction? paintHint,
        InteractionKind kind,
        GameMode mode,
        string? heldItemId,
        long simTick,
        float fixedDelta
    ) {
        //  The hint and the mirror transform are the only client-authored inputs; the pipeline resolves
        //  the rest against the authority store.
        var request = new InteractionRequest(
            mirror.Position,
            brickHint,
            kind,
            null,
            mode,
            CharacterDefaults.DEFAULT_REACH,
            paintHint
        );

        return new InteractionContext(store, player, clientId, sequence, world, in request, heldItemId, simTick, fixedDelta);
    }

    private static GameMode GetGameMode(DataStore store, int player)
    {
        return store.TryGet(player, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Creative;
    }

    private static bool MatchesSession(
        in TimedSession existing,
        in InteractionHold incoming,
        int activeSlot,
        string? heldItemId
    ) {
        if (existing.Key != incoming.Key)
        {
            return false;
        }

        if (!incoming.CancelOnEquipmentChange)
        {
            return true;
        }

        return existing.ActiveSlot == activeSlot &&
            string.Equals(existing.HeldItemId, heldItemId, StringComparison.Ordinal);
    }

    private static void TryGetHeldEquipment(DataStore store, int player, out int activeSlot, out string? heldItemId)
    {
        if (!store.TryGet(player, out EquipmentComponent equipment)
            || !store.TryGet(player, out InventoryComponent inventory))
        {
            activeSlot = -1;
            heldItemId = null;
            return;
        }

        activeSlot = equipment.ActiveInventorySlot;
        ItemData item = inventory.Get(new SlotId(SlotGroups.HOTBAR, activeSlot));
        heldItemId = item.IsEmpty() ? null : item.ID;
    }

    /// <summary>Snapshot of an active server timed interaction session for diagnostics and testing.</summary>
    internal readonly record struct TimedSessionState(
        TimedInteractionKey Key,
        int ActiveSlot,
        string? HeldItemId,
        float Elapsed,
        float RequiredDuration
    )
    {
        /// <summary>Normalized hold completion in <c>[0..1]</c>.</summary>
        public float Progress => TimedInteractionProgress.Normalize(Elapsed, RequiredDuration);
    }

    private struct TimedSession
    {
        public TimedInteractionKey Key;
        public InteractionInput RequiredInput;
        public bool CancelOnEquipmentChange;
        public BrickInteraction? BrickHint;
        public PaintInteraction? PaintHint;
        public InteractionKind Kind;
        public int ActiveSlot;
        public string? HeldItemId;
        public uint Sequence;
        public float Elapsed;
        public float RequiredDuration;
    }

    private struct ConsumeAction : IForEachRef<NetworkComponent, OwnedCharacterComponent, TransformComponent>
    {
        public ServerInteractionSystem Owner;
        public DataStore Store;
        public uint SimTick;

        public void Execute(float delta, DataStore store, int entity, ref Ref<NetworkComponent> net, ref Ref<OwnedCharacterComponent> owned, ref Ref<TransformComponent> transform)
        {
            ref readonly NetworkComponent current = ref net.Read;
            ref readonly TransformComponent transformValue = ref transform.Read;

            Owner.ProcessPlayer(Store, entity, delta, SimTick, in current, in transformValue, out uint consumedSequence);

            //  Change-gated: a write of an unchanged value still marks the component dirty.
            if (consumedSequence != current.LastConsumedInteractionSequence)
            {
                net.Write.LastConsumedInteractionSequence = consumedSequence;
            }
        }
    }
}
