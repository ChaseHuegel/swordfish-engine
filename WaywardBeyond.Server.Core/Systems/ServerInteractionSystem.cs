using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Server.Core.Systems;

/// <summary>
/// The authoritative interaction system. Runs once per server tick, between the replication apply stage
/// (which drains inbound client interaction events into each player mirror's staged buffer) and the
/// publish stage. For each staged interaction at or below the current sim tick it:
/// builds an authority ray from the mirror's settled transform + look, resolves the interaction with the
/// shared <see cref="SharedInteractionResolver"/>, and applies the outcome on the target structure's live
/// <see cref="VoxelWorldComponent"/> - rebuilding the collider and re-deriving the persisted chunks.
/// Survival consumption/loot is applied against the server-owned <see cref="InventoryComponent"/>;
/// creative mode is free. A hint-less or rejected interaction resolves to <see cref="InteractionAction.None"/>
/// and is simply skipped.
/// </summary>
public sealed class ServerInteractionSystem
{
    private readonly ILogger<ServerInteractionSystem> _logger;
    private readonly IInteractionContent _content;
    private readonly IPhysics _physics;
    private readonly Func<DataStore, IVoxelInteractionWorld> _worldFactory;

    private readonly Dictionary<int, uint> _lastConsumedSequences = [];

    public ServerInteractionSystem(
        in IPhysics physics,
        in IInteractionContent content,
        ILogger<ServerInteractionSystem> logger
    ) : this(physics, content, logger, CreateWorldFactory(in physics)) { }

    public ServerInteractionSystem(
        in IPhysics physics,
        in IInteractionContent content,
        ILogger<ServerInteractionSystem> logger,
        Func<DataStore, IVoxelInteractionWorld> worldFactory
    ) {
        _physics = physics;
        _content = content;
        _logger = logger;
        _worldFactory = worldFactory;
    }

    private static Func<DataStore, IVoxelInteractionWorld> CreateWorldFactory(in IPhysics physics)
    {
        IPhysics captured = physics;
        return store => new ServerVoxelInteractionWorld(store, captured);
    }

    /// <summary>Processes staged interactions for the given sim tick on the authoritative server world.</summary>
    public void Tick(float delta, DataStore store, uint simTick)
    {
        ConsumeAction action = new() { Owner = this, Store = store, SimTick = simTick };
        store.Query<NetworkComponent, OwnedCharacterComponent, TransformComponent, ConsumeAction>(0f, ref action);
    }

    private void Consume(DataStore store, int entity, uint simTick, in NetworkComponent net, in TransformComponent transform)
    {
        if (net.StagedInteractions == null)
        {
            return;
        }

        uint lastSequence = _lastConsumedSequences.GetValueOrDefault(entity);
        InteractionStageBuffer buffer = net.StagedInteractions;

        while (buffer.TryConsume(simTick, lastSequence, out InteractionEvent interaction))
        {
            lastSequence = interaction.SequenceNumber;
            ProcessInteraction(store, entity, in interaction, in transform);
        }

        _lastConsumedSequences[entity] = lastSequence;
    }

    private void ProcessInteraction(DataStore store, int player, in InteractionEvent interaction, in TransformComponent mirror)
    {
        var kind = (InteractionKind)interaction.Kind;

        //  Resolve the held item to the placeable brick it places, if any (the resolver rejects a
        //  place hint without one; a break never needs it).
        string? heldItemID = GetHeldItemID(store, player);
        PlaceableBrick? placeable = null;
        if (heldItemID != null && _content.TryGetPlaceable(heldItemID, out PlaceableBrick resolved))
        {
            placeable = resolved;
        }

        GameMode mode = GameMode.Creative;
        if (store.TryGet(player, out GameModeComponent gameMode))
        {
            mode = gameMode.Mode;
        }

        //  Authority ray from the settled mirror transform + look (already applied server-side by the
        //  shared step during the physics tick that precedes us in the server tick loop).
        Ray ray = new(mirror.Position, mirror.GetForward());
        IVoxelInteractionWorld world = _worldFactory(store);

        InteractionResolution resolution = SharedInteractionResolver.Resolve(ray, interaction.Brick, kind, placeable, mode, SharedInteractionResolver.DEFAULT_REACH, world);
        if (resolution.Action == InteractionAction.None)
        {
            return;
        }

        if (!store.TryGet(resolution.Entity, out VoxelWorldComponent voxelWorldComponent))
        {
            _logger.LogWarning("Resolved interaction on entity {entity} but it no longer has a live voxel container.", resolution.Entity);
            return;
        }

        VoxelObject voxelObject = voxelWorldComponent.VoxelObject;
        Int3 coordinate = resolution.Coordinate;

        switch (resolution.Action)
        {
            case InteractionAction.Break:
                voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, new Voxel());
                GrantLoot(store, player, mode, resolution.Voxel.ID);
                break;

            case InteractionAction.Place:
                voxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, resolution.Voxel);
                ConsumeHeldItem(store, player, mode);
                break;
        }

        //  Rebuild the structure's collider so subsequent authority raycasts see the change, and
        //  re-derive the persisted chunks so the next world save reflects the edit.
        store.AddOrUpdate(resolution.Entity, new ColliderComponent(VoxelColliderBuilder.BuildCollition(voxelObject.GetChunkInfos())));
        store.AddOrUpdate(resolution.Entity, new VoxelEntityDataComponent(voxelObject.GetChunkInfos()));
        store.MarkDirty<VoxelEntityDataComponent>(resolution.Entity);

        _logger.LogDebug("Applied {action} on entity {entity} at {coordinate} for player {player}.", resolution.Action, resolution.Entity, coordinate, player);
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

        if (!store.TryGet(player, out InventoryComponent inventory) ||
            !_content.TryGetLoot(brokenBrickDataID, out ItemData loot))
        {
            return;
        }

        InventoryComponent updated = inventory;
        updated.Add(loot);
        store.AddOrUpdate(player, updated);
        store.MarkDirty<InventoryComponent>(player);
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
        store.MarkDirty<InventoryComponent>(player);
    }

    private struct ConsumeAction : IForEach<NetworkComponent, OwnedCharacterComponent, TransformComponent>
    {
        public ServerInteractionSystem Owner;
        public DataStore Store;
        public uint SimTick;

        public void Execute(float delta, DataStore store, int entity, in NetworkComponent net, in OwnedCharacterComponent owned, in TransformComponent transform)
        {
            if (net.StagedInteractions == null)
            {
                return;
            }

            Owner.Consume(Store, entity, SimTick, in net, in transform);
        }
    }
}

/// <summary>
/// Server-side <see cref="IVoxelInteractionWorld"/>: raycasts the server's Jolt world (whose structure
/// colliders are built from the shared <see cref="VoxelColliderBuilder"/>) and reads a structure's live
/// voxel container + transform from the authority store.
/// </summary>
public sealed class ServerVoxelInteractionWorld(DataStore store, IPhysics physics) : IVoxelInteractionWorld
{
    public bool TryRaycast(in Ray ray, out RaycastResult result)
    {
        result = physics.Raycast(ray);
        return result.Hit;
    }

    public bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform)
    {
        if (store.TryGet(entity, out VoxelWorldComponent world) && store.TryGet(entity, out TransformComponent transformComponent))
        {
            voxelObject = world.VoxelObject;
            transform = transformComponent;
            return true;
        }

        voxelObject = null;
        transform = default;
        return false;
    }
}