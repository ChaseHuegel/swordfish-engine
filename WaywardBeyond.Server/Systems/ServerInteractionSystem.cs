using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Skills;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// The authoritative interaction pump. Runs once per server tick, between the replication apply stage
/// (which drains inbound client interaction events into each player mirror's staged buffer) and the
/// publish stage. In Creative mode interactions resolve and commit immediately; in Adventure mode
/// brick breaking advances a server-validated timed hold session before committing removal.
/// </summary>
public sealed class ServerInteractionSystem(
    SessionManager sessions,
    ILogger<ServerInteractionSystem> logger,
    IInteractionHandlerRegistry handlerRegistry,
    Func<DataStore, IVoxelInteractionWorld> worldFactory,
    SimulationStep simulationStep,
    IBrickDatabase? brickDatabase = null,
    IItemDatabase? itemDatabase = null,
    ISkillDatabase? skillDatabase = null
) : IServerWorldSystem
{
    private readonly Dictionary<Uuid, BreakSession> _breakSessions = [];
    private readonly HashSet<Uuid> _visitedPlayers = [];
    private readonly List<Uuid> _stalePlayers = [];

    /// <summary>Snapshot of an active Adventure Mode break session for diagnostics and testing.</summary>
    internal readonly record struct BreakSessionState(
        Uuid TargetEntityUuid,
        Int3 TargetCoord,
        ushort TargetBrickDataId,
        int ActiveSlot,
        string? HeldItemId,
        float Elapsed,
        float RequiredDuration
    )
    {
        /// <summary>Normalized hold completion in <c>[0..1]</c>.</summary>
        public float Progress => BreakDuration.NormalizeProgress(Elapsed, RequiredDuration);
    }

    private struct BreakSession
    {
        public Uuid TargetEntityUuid;
        public Int3 TargetCoord;
        public ushort TargetBrickDataId;
        public int ActiveSlot;
        public string? HeldItemId;
        public uint Sequence;
        public float Elapsed;
        public float RequiredDuration;
    }

    /// <summary>Gets the active Adventure Mode break session for a player mirror, if one is running.</summary>
    internal Result<BreakSessionState> GetBreakSession(Uuid playerUuid)
    {
        if (_breakSessions.TryGetValue(playerUuid, out BreakSession session))
        {
            return Result<BreakSessionState>.FromSuccess(new BreakSessionState(
                session.TargetEntityUuid,
                session.TargetCoord,
                session.TargetBrickDataId,
                session.ActiveSlot,
                session.HeldItemId,
                session.Elapsed,
                session.RequiredDuration));
        }

        return Result<BreakSessionState>.FromFailure("No active break session.");
    }

    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        Tick(delta, store, simulationStep.CurrentSimTick);
    }

    /// <summary>Processes staged interactions and advances Adventure break holds for <paramref name="simTick"/>.</summary>
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

        AdvanceBreakSession(store, entity, playerUuid, delta, simTick, in net, in transform);
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

        if (mode != GameMode.Adventure)
        {
            _breakSessions.Remove(playerUuid);
            RunFullInteraction(store, player, in interaction, in mirror, mode, kind);
            return;
        }

        if (kind == InteractionKind.PrimaryReleased)
        {
            _breakSessions.Remove(playerUuid);
            RunFullInteraction(store, player, in interaction, in mirror, mode, kind);
            return;
        }

        TryGetHeldEquipment(store, player, out int activeSlot, out string? heldItemId);
        IVoxelInteractionWorld world = worldFactory(store);
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
            heldItemId);

        InteractionRunResult preCheck = handlerRegistry.Run(ref context, InteractionStage.Select, InteractionStage.Validate);
        if (preCheck.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
        {
            if (kind == InteractionKind.PrimaryPressed)
            {
                _breakSessions.Remove(playerUuid);
            }

            logger.LogDebug(
                "Interaction {sequence} for player {player} ended with {decision}.",
                interaction.SequenceNumber, player, preCheck.Decision);
            return;
        }

        if (preCheck.Resolution.Action == InteractionAction.None)
        {
            if (kind == InteractionKind.PrimaryPressed)
            {
                _breakSessions.Remove(playerUuid);
            }

            logger.LogDebug(
                "Interaction {sequence} for player {player} resolved to nothing.",
                interaction.SequenceNumber, player);
            return;
        }

        if (preCheck.Resolution.Action != InteractionAction.Break)
        {
            if (kind == InteractionKind.PrimaryPressed)
            {
                _breakSessions.Remove(playerUuid);
            }

            handlerRegistry.Run(ref context, InteractionStage.Commit, InteractionStage.React);
            return;
        }

        if (!IsPrimaryHeld(store, player, in net, simTick))
        {
            _breakSessions.Remove(playerUuid);
            return;
        }

        Uuid targetUuid = interaction.Brick.HasValue
            ? Uuid.FromValue(interaction.Brick.Value.TargetEntity)
            : store.GetUuid(preCheck.Resolution.Entity);

        float requiredDuration = ResolveRequiredDuration(store, player, preCheck.Resolution.Voxel.ID, heldItemId);
        if (_breakSessions.TryGetValue(playerUuid, out BreakSession existing) &&
            MatchesSessionTarget(in existing, targetUuid, in preCheck.Resolution.Coordinate, preCheck.Resolution.Voxel.ID, activeSlot, heldItemId))
        {
            existing.Sequence = interaction.SequenceNumber;
            existing.RequiredDuration = requiredDuration;
            _breakSessions[playerUuid] = existing;
            return;
        }

        _breakSessions[playerUuid] = new BreakSession
        {
            TargetEntityUuid = targetUuid,
            TargetCoord = preCheck.Resolution.Coordinate,
            TargetBrickDataId = preCheck.Resolution.Voxel.ID,
            ActiveSlot = activeSlot,
            HeldItemId = heldItemId,
            Sequence = interaction.SequenceNumber,
            Elapsed = 0f,
            RequiredDuration = requiredDuration,
        };
    }

    private void AdvanceBreakSession(
        DataStore store,
        int entity,
        Uuid playerUuid,
        float delta,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform
    ) {
        if (!_breakSessions.TryGetValue(playerUuid, out BreakSession session))
        {
            return;
        }

        if (GetGameMode(store, entity) != GameMode.Adventure || !IsPrimaryHeld(store, entity, in net, simTick))
        {
            _breakSessions.Remove(playerUuid);
            return;
        }

        TryGetHeldEquipment(store, entity, out int activeSlot, out string? heldItemId);
        if (activeSlot != session.ActiveSlot || !string.Equals(heldItemId, session.HeldItemId, StringComparison.Ordinal))
        {
            _breakSessions.Remove(playerUuid);
            return;
        }

        var hint = new BrickInteraction
        {
            TargetEntity = session.TargetEntityUuid.ToValue(),
            TargetX = session.TargetCoord.X,
            TargetY = session.TargetCoord.Y,
            TargetZ = session.TargetCoord.Z,
        };
        IVoxelInteractionWorld world = worldFactory(store);
        Uuid clientId = sessions.TryGetClient(entity, out Uuid client) ? client : Uuid.Null;
        InteractionContext context = BuildContext(
            store,
            entity,
            clientId,
            session.Sequence,
            world,
            in transform,
            hint,
            paintHint: null,
            InteractionKind.PrimaryPressed,
            GameMode.Adventure,
            heldItemId);

        InteractionRunResult validation = handlerRegistry.Run(ref context, InteractionStage.Select, InteractionStage.Validate);
        if (validation.Decision is InteractionDecision.Consume or InteractionDecision.Cancel ||
            validation.Resolution.Action != InteractionAction.Break ||
            validation.Resolution.Coordinate != session.TargetCoord ||
            validation.Resolution.Voxel.ID != session.TargetBrickDataId)
        {
            _breakSessions.Remove(playerUuid);
            return;
        }

        float requiredDuration = ResolveRequiredDuration(store, entity, session.TargetBrickDataId, heldItemId);
        float elapsed = session.Elapsed + delta;

        if (BreakDuration.IsComplete(elapsed, requiredDuration))
        {
            _breakSessions.Remove(playerUuid);
            handlerRegistry.Run(ref context, InteractionStage.Commit, InteractionStage.React);
            return;
        }

        session.Elapsed = elapsed;
        session.RequiredDuration = requiredDuration;
        _breakSessions[playerUuid] = session;
    }

    private void RunFullInteraction(
        DataStore store,
        int player,
        in InteractionEvent interaction,
        in TransformComponent mirror,
        GameMode mode,
        InteractionKind kind
    ) {
        TryGetHeldEquipment(store, player, out _, out string? heldItemId);
        IVoxelInteractionWorld world = worldFactory(store);
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
            heldItemId);

        InteractionRunResult result = handlerRegistry.Run(ref context);
        if (result.Decision is InteractionDecision.Consume or InteractionDecision.Cancel)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} ended with {decision}.",
                interaction.SequenceNumber, player, result.Decision);
            return;
        }

        if (result.Resolution.Action == InteractionAction.None)
        {
            logger.LogDebug(
                "Interaction {sequence} for player {player} resolved to nothing.",
                interaction.SequenceNumber, player);
        }
    }

    private float ResolveRequiredDuration(DataStore store, int player, ushort brickDataId, string? heldItemId)
    {
        IReadOnlyDictionary<string, long>? xpBySkill = store.TryGet(player, out SkillStateComponent skillState)
            ? skillState.XPBySkill
            : null;
        return BreakDuration.ResolveBreakDuration(brickDatabase, itemDatabase, skillDatabase, brickDataId, heldItemId, xpBySkill);
    }

    private bool IsPrimaryHeld(DataStore store, int entity, in NetworkComponent net, uint simTick)
    {
        if (net.StagedInputs != null && net.StagedInputs.Count > 0)
        {
            Result<InputComponent> staged = net.StagedInputs.GetCommand(simTick, simulationStep.CommandMaxAgeTicks);
            return staged.Success && staged.Value.PrimaryHeld;
        }

        return store.TryGet(entity, out InputComponent latestInput) && latestInput.PrimaryHeld;
    }

    private void PruneStaleSessions()
    {
        if (_breakSessions.Count == 0)
        {
            return;
        }

        _stalePlayers.Clear();
        foreach (Uuid playerUuid in _breakSessions.Keys)
        {
            if (!_visitedPlayers.Contains(playerUuid))
            {
                _stalePlayers.Add(playerUuid);
            }
        }

        for (var i = 0; i < _stalePlayers.Count; i++)
        {
            _breakSessions.Remove(_stalePlayers[i]);
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
        string? heldItemId
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

        return new InteractionContext(store, player, clientId, sequence, world, in request, heldItemId);
    }

    private static GameMode GetGameMode(DataStore store, int player)
    {
        return store.TryGet(player, out GameModeComponent gameMode) ? gameMode.Mode : GameMode.Creative;
    }

    private static bool MatchesSessionTarget(
        in BreakSession existing,
        Uuid targetUuid,
        in Int3 coord,
        ushort brickDataId,
        int activeSlot,
        string? heldItemId
    ) {
        return existing.TargetEntityUuid == targetUuid &&
            existing.TargetCoord == coord &&
            existing.TargetBrickDataId == brickDataId &&
            existing.ActiveSlot == activeSlot &&
            string.Equals(existing.HeldItemId, heldItemId, StringComparison.Ordinal);
    }

    private static void TryGetHeldEquipment(DataStore store, int player, out int activeSlot, out string? heldItemId)
    {
        if (!store.TryGet(player, out EquipmentComponent equipment) ||
            !store.TryGet(player, out InventoryComponent inventory) ||
            inventory.Contents == null)
        {
            activeSlot = -1;
            heldItemId = null;
            return;
        }

        int slot = equipment.ActiveInventorySlot;
        activeSlot = slot;
        if (slot < 0 || slot >= inventory.Contents.Length)
        {
            heldItemId = null;
            return;
        }

        ItemData item = inventory.Contents[slot];
        heldItemId = item.Count > 0 && !string.IsNullOrEmpty(item.ID) ? item.ID : null;
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
