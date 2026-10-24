using System;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// The authoritative interaction system. Runs once per server tick, between the replication apply stage
/// (which drains inbound client interaction events into each player mirror's staged buffer) and the
/// publish stage. For each staged interaction at or below the current sim tick it:
/// builds an authority ray from the mirror's settled transform + look, resolves the interaction with the
/// shared <see cref="WaywardBeyond.Gameplay.Interactions.InteractionResolver"/>, and applies the outcome on the target structure's live
/// <see cref="VoxelWorldComponent"/> - rebuilding the collider and re-deriving the persisted chunks.
/// Survival consumption/loot is applied against the server-owned <see cref="InventoryComponent"/>;
/// creative mode is free. A hint-less or rejected interaction resolves to <see cref="InteractionAction.None"/>
/// and is simply skipped.
/// </summary>
public sealed class ServerInteractionSystem(
    ServerPeers peers,
    SessionManager sessions,
    IInteractionContent content,
    ILogger<ServerInteractionSystem> logger,
    IInteractionHandlerRegistry handlerRegistry,
    Func<DataStore, IVoxelInteractionWorld> worldFactory,
    IBrickRegistry brickRegistry,
    ServerSkillSystem skills,
    SimulationStep simulationStep
) : IServerWorldSystem
{
    /// <inheritdoc/>
    public void Tick(float delta, DataStore store)
    {
        Tick(delta, store, simulationStep.CurrentSimTick);
    }

    /// <summary>Processes staged interactions for the given sim tick on the authoritative server world.</summary>
    public void Tick(float delta, DataStore store, uint simTick)
    {
        ConsumeAction action = new() { Owner = this, Store = store, SimTick = simTick };
        store.QueryRef<NetworkComponent, OwnedCharacterComponent, TransformComponent, ConsumeAction>(0f, ref action);
    }

    private void Consume(
        DataStore store,
        int entity,
        uint simTick,
        in NetworkComponent net,
        in TransformComponent transform,
        out uint consumedSequence
    ) {
        consumedSequence = net.LastConsumedInteractionSequence;

        if (net.StagedInteractions == null)
        {
            return;
        }

        StageRing<InteractionEvent> buffer = net.StagedInteractions;

        Result<InteractionEvent> staged = buffer.Consume(simTick, consumedSequence);
        while (staged.Success)
        {
            InteractionEvent interaction = staged.Value;
            consumedSequence = interaction.SequenceNumber;
            ProcessInteraction(store, entity, in interaction, in transform);
            staged = buffer.Consume(simTick, consumedSequence);
        }
    }

    private void ProcessInteraction(DataStore store, int player, in InteractionEvent interaction, in TransformComponent mirror)
    {
        var kind = (InteractionKind)interaction.Kind;

        //  Resolve the held item to the placeable brick it places, if any (the resolver rejects a
        //  place hint without one; a break never needs it).
        string? heldItemID = GetHeldItemID(store, player);
        Brick? placeable = null;
        if (heldItemID != null)
        {
            Result<Brick> placeableResult = content.GetPlaceable(heldItemID);
            if (placeableResult.Success)
            {
                placeable = placeableResult.Value;
            }
        }

        GameMode mode = GameMode.Creative;
        if (store.TryGet(player, out GameModeComponent gameMode))
        {
            mode = gameMode.Mode;
        }

        //  Validate the hinted structure + cell against the authority store purely by identity and reach;
        //  no raycasting is needed (the client's screen-aim targeting produced the hint).
        Vector3 origin = mirror.Position;
        IVoxelInteractionWorld world = worldFactory(store);

        InteractionRequest request = new(origin, interaction.Brick, kind, placeable, mode, CharacterDefaults.DEFAULT_REACH);
        InteractionResolution resolution = InteractionResolver.Resolve(in request, world, brickRegistry);
        if (resolution.Action == InteractionAction.None)
        {
            return;
        }

        //  Registered handlers run after base validation and may reject or override the outcome.
        resolution = handlerRegistry.Apply(request, resolution, heldItemID);
        if (resolution.Action == InteractionAction.None)
        {
            return;
        }

        if (!store.TryGet(resolution.Entity, out VoxelWorldComponent voxelWorldComponent))
        {
            logger.LogWarning("Resolved interaction on entity {entity} but it no longer has a live voxel container.", resolution.Entity);
            return;
        }

        VoxelObject voxelObject = voxelWorldComponent.VoxelObject;
        Int3 coordinate = resolution.Coordinate;

        switch (resolution.Action)
        {
            case InteractionAction.Break:
                voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, new Voxel());
                GrantLoot(store, player, mode, resolution.Voxel.ID);
                skills.OnInteractionApplied(store, player, resolution.Voxel.ID, isBreak: true);
                break;

            case InteractionAction.Place:
                voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, resolution.Voxel);
                ConsumeHeldItem(store, player, mode);
                skills.OnInteractionApplied(store, player, resolution.Voxel.ID, isBreak: false);
                break;
        }

        //  Rebuild the structure's collider so subsequent authority raycasts see the change. The live
        //  VoxelObject is the single voxel representation; saves and streams re-derive chunks from it.
        store.AddOrUpdate(resolution.Entity, new ColliderComponent(VoxelColliderBuilder.BuildCollision(voxelObject.GetChunkInfos())));

        Voxel newVoxel = voxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        BroadcastEdit(store, resolution.Entity, coordinate, newVoxel, interaction.SequenceNumber);

        logger.LogDebug("Applied {action} on entity {entity} at {coordinate} for player {player}.", resolution.Action, resolution.Entity, coordinate, player);
    }

    /// <summary>
    /// Broadcasts an authoritative voxel edit to every connected client so both the origin client and
    /// remote witnesses apply the delta via the same shared voxel container. Carries the originating
    /// interaction's <paramref name="sequence"/> so the origin client can resolve the exact prediction
    /// it echoed, instead of correlating by (entity, coordinate).
    /// </summary>
    private void BroadcastEdit(DataStore store, int entity, Int3 coordinate, in Voxel voxel, uint sequence)
    {
        var message = new VoxelEditMessage
        {
            EntityUuid = store.GetUuid(entity).ToValue(),
            X = coordinate.X,
            Y = coordinate.Y,
            Z = coordinate.Z,
            Voxel = voxel,
            Sequence = sequence,
            BrickId = brickRegistry.Name(voxel.ID),
        };

        foreach (Uuid clientId in sessions.Clients)
        {
            if (peers.TryGet(clientId, out ServerPeer peer) && !peer.Rpc.Notify(GameRpc.Methods.VOXEL_EDIT, in message))
            {
                peer.CloseOnRejectedSend(logger);
            }
        }
    }

    private string? GetHeldItemID(DataStore store, int player)
    {
        if (!store.TryGet(player, out EquipmentComponent equipment) ||
            !store.TryGet(player, out InventoryComponent inventory))
        {
            return null;
        }

        int slot = equipment.ActiveInventorySlot;
        if (slot < 0 || slot >= inventory.Contents.Length)
        {
            return null;
        }

        return inventory.Contents[slot].ID;
    }

    /// <summary>
    /// Applies the survival-mode break loot against the server-owned inventory. Creative mode is free.
    /// </summary>
    private void GrantLoot(DataStore store, int player, GameMode mode, ushort brokenBrickDataID)
    {
        if (mode == GameMode.Creative)
        {
            return;
        }

        if (!store.TryGet(player, out InventoryComponent inventory))
        {
            return;
        }

        Result<ItemData> lootResult = content.GetLoot(brokenBrickDataID);
        if (!lootResult.Success)
        {
            return;
        }

        InventoryComponent updated = inventory;
        updated.Add(lootResult.Value);
        store.AddOrUpdate(player, updated);
    }

    /// <summary>
    /// Applies the survival-mode place consumption against the server-owned inventory. Creative is free.
    /// </summary>
    private void ConsumeHeldItem(DataStore store, int player, GameMode mode)
    {
        if (mode == GameMode.Creative)
        {
            return;
        }

        if (!store.TryGet(player, out EquipmentComponent equipment) ||
            !store.TryGet(player, out InventoryComponent inventory))
        {
            return;
        }

        int slot = equipment.ActiveInventorySlot;
        if (slot < 0 || slot >= inventory.Contents.Length)
        {
            return;
        }

        InventoryComponent updated = inventory;
        updated.Remove(slot, 1);
        store.AddOrUpdate(player, updated);
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

            Owner.Consume(Store, entity, SimTick, in current, in transformValue, out uint consumedSequence);

            //  Change-gated: a write of an unchanged value still marks the component dirty.
            if (consumedSequence != current.LastConsumedInteractionSequence)
            {
                net.Write.LastConsumedInteractionSequence = consumedSequence;
            }
        }
    }
}
