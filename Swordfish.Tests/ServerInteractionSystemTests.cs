using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// 4.2 acceptance, headless over a deterministic server world (no window). The server interaction system
/// is the sole author of voxel edits: it consumes a staged interaction for the current sim tick, resolves
/// the shared resolver against the authority world, mutates the structure's live VoxelWorldComponent,
/// rebuilds the collider + re-derives the persisted chunks, and applies survival consumption/loot against
/// the server-owned inventory (creative is free). A deterministic <see cref="IVoxelInteractionWorld"/>
/// supplies the raycast so the resolver paths run without a live physics world.
/// </summary>
public class ServerInteractionSystemTests
{
    private const ushort BRICK_DATA_ID = 7;

    [Fact]
    public void SurvivalBreakRemovesVoxelGrantsLootAndRebuildsCollider()
    {
        DataStore store = BuildWorld(out int structure, out VoxelObject voxelObject);

        int player = BuildPlayerMirror(store, GameMode.Adventure, heldItemID: "laser");
        StageInteraction(store, player, kind: InteractionKind.PrimaryPressed, hint: Hint(0, 0, 0));

        var physics = new StubPhysics();
        ServerInteractionSystem system = new(physics, new StubContent(), NullLogger<ServerInteractionSystem>.Instance, _ => new StubWorld(structure, voxelObject));

        system.Tick(0f, store, simTick: 100);

        //  The break emptied the target cell.
        Assert.Equal((ushort)0, voxelObject.Get(0, 0, 0).ID);

        //  Survival granted the loot for the broken brick.
        Assert.True(store.TryGet(player, out InventoryComponent inventory));
        Assert.Contains(inventory.Contents, stack => stack.ID == "rock" && stack.Count == 1);

        //  The collider was rebuilt from the (now empty) voxel content.
        Assert.True(store.TryGet(structure, out ColliderComponent collider));
        Assert.True(collider.CompoundShape.HasValue);
        Assert.Empty(collider.CompoundShape.Value.Shapes);

        //  The persisted chunk content tracks the live edit.
        Assert.True(store.TryGet(structure, out VoxelEntityDataComponent data));
        Assert.Single(data.Chunks);
        Assert.Equal((ushort)0, data.Chunks[0].Chunk.Voxels[0].ID);
    }

    [Fact]
    public void SurvivalPlaceConsumesHeldItemAndWritesVoxel()
    {
        DataStore store = BuildWorld(out int structure, out VoxelObject voxelObject);

        int player = BuildPlayerMirror(store, GameMode.Adventure, heldItemID: "panel");
        StoreInitialVoxel(voxelObject, 0, 0, 0);
        StageInteraction(store, player, kind: InteractionKind.SecondaryPressed, hint: Hint(1, 0, 0));

        var physics = new StubPhysics();
        ServerInteractionSystem system = new(physics, new StubContent(), NullLogger<ServerInteractionSystem>.Instance, _ => new StubWorld(structure, voxelObject));

        system.Tick(0f, store, simTick: 100);

        //  The destination cell received the placed voxel.
        Assert.Equal(BRICK_DATA_ID, voxelObject.Get(1, 0, 0).ID);

        //  Survival consumed one held item.
        Assert.True(store.TryGet(player, out InventoryComponent inventory));
        Assert.Equal("panel", inventory.Contents[0].ID);
        Assert.Equal(4, inventory.Contents[0].Count);
    }

    [Fact]
    public void CreativePlaceAndBreakAreFree()
    {
        DataStore store = BuildWorld(out int structure, out VoxelObject voxelObject);

        int player = BuildPlayerMirror(store, GameMode.Creative, heldItemID: "panel");
        StoreInitialVoxel(voxelObject, 0, 0, 0);
        StageInteraction(store, player, kind: InteractionKind.SecondaryPressed, hint: Hint(1, 0, 0));
        StageInteraction(store, player, kind: InteractionKind.PrimaryPressed, hint: Hint(0, 0, 0));

        var physics = new StubPhysics();
        ServerInteractionSystem system = new(physics, new StubContent(), NullLogger<ServerInteractionSystem>.Instance, _ => new StubWorld(structure, voxelObject));

        //  Tick once for each interaction (they share a sequence space; stagger sequences).
        system.Tick(0f, store, simTick: 100);

        //  Place happened (creative consumes nothing) and break cleared a fresh voxel.
        Assert.Equal(BRICK_DATA_ID, voxelObject.Get(1, 0, 0).ID);
        Assert.Equal(BRICK_DATA_ID, voxelObject.Get(0, 0, 0).ID);

        //  The inventory was untouched by creative interactions.
        Assert.True(store.TryGet(player, out InventoryComponent inventory));
        Assert.Equal(5, inventory.Contents[0].Count);
    }

    [Fact]
    public void RejectedOrHintLessInteractionLeavesWorldUntouched()
    {
        DataStore store = BuildWorld(out int structure, out VoxelObject voxelObject);

        int player = BuildPlayerMirror(store, GameMode.Adventure, heldItemID: "rock");
        StageInteraction(store, player, kind: InteractionKind.PrimaryPressed, hint: null);

        var physics = new StubPhysics();
        ServerInteractionSystem system = new(physics, new StubContent(), NullLogger<ServerInteractionSystem>.Instance, _ => new StubWorld(structure, voxelObject));

        system.Tick(0f, store, simTick: 100);

        //  The untouched cell (seeded at (0,0,0)) keeps its voxel; a hint-less event resolves to none.
        Assert.Equal(BRICK_DATA_ID, voxelObject.Get(0, 0, 0).ID);

        //  The interaction was marked consumed so it won't re-fire next tick.
        Assert.True(store.TryGet(player, out NetworkComponent net));
        Assert.False(net.StagedInteractions!.TryConsume(100, lastSequenceNumber: 1, out _));
    }

    private DataStore BuildWorld(out int structure, out VoxelObject voxelObject)
    {
        var store = new DataStore();
        voxelObject = new VoxelObject(chunkSize: 16);
        StoreInitialVoxel(voxelObject, BRICK_DATA_ID, x: 0, y: 0, z: 0);
        store.AddOrUpdate(structure = store.Alloc(), new VoxelWorldComponent(voxelObject));
        store.AddOrUpdate(structure, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(structure, new ColliderComponent(VoxelColliderBuilder.BuildCollition(voxelObject.GetChunkInfos())));
        store.AddOrUpdate(structure, new VoxelEntityDataComponent(voxelObject.GetChunkInfos()));
        return store;
    }

    private int BuildPlayerMirror(DataStore store, GameMode mode, string heldItemID)
    {
        int player = store.Alloc();
        store.AddOrUpdate(player, new OwnedCharacterComponent(1));
        store.AddOrUpdate(player, new NetworkComponent());
        store.AddOrUpdate(player, new EquipmentComponent(0));
        store.AddOrUpdate(player, new GameModeComponent(mode));
        var inventory = new InventoryComponent();
        inventory.Contents[0] = new ItemData { ID = heldItemID, Count = 5, MaxSize = 100 };
        store.AddOrUpdate(player, inventory);
        store.AddOrUpdate(player, new TransformComponent(new Vector3(-3f, 0f, 0f), Quaternion.Identity, Vector3.One));
        NetworkComponent net = new();
        net.StagedInteractions = new InteractionStageBuffer();
        store.AddOrUpdate(player, net);
        return player;
    }

    private static void StageInteraction(DataStore store, int player, InteractionKind kind, BrickInteraction? hint)
    {
        Assert.True(store.TryGet(player, out NetworkComponent net));
        uint sequence = (uint)(kind == InteractionKind.SecondaryPressed ? 2 : 1);
        net.StagedInteractions!.Stage(new InteractionEvent
        {
            SequenceNumber = sequence,
            ServerTickAtSample = 100,
            Kind = (byte)kind,
            Brick = hint,
        });
    }

    private static BrickInteraction Hint(int x, int y, int z)
    {
        return new BrickInteraction
        {
            TargetX = x,
            TargetY = y,
            TargetZ = z,
            HintShape = 0,
            HintOrientation = 0,
        };
    }

    private static void StoreInitialVoxel(VoxelObject voxelObject, ushort id, int x, int y, int z)
    {
        voxelObject.Set(x, y, z, new Voxel(id, 0, 0));
    }

    private static void StoreInitialVoxel(VoxelObject voxelObject, int x, int y, int z)
    {
        StoreInitialVoxel(voxelObject, BRICK_DATA_ID, x, y, z);
    }

    /// <summary>
    /// Canned world: any ray hits the single occupied cell at (0,0,0) on its +X face, so the resolver
    /// derives cell (0,0,0) for a break and the adjacent (1,0,0) for a place - deterministic headlessly.
    /// </summary>
    private sealed class StubWorld(int structure, VoxelObject voxelObject) : IVoxelInteractionWorld
    {
        public bool TryRaycast(in Ray ray, out RaycastResult result)
        {
            result = new RaycastResult(true, new Entity(structure, null!), new Vector3(0.5f, 0f, 0f), Vector3.UnitX);
            return true;
        }

        public bool TryGetVoxelTarget(int entity, out VoxelObject? voxel, out TransformComponent transform)
        {
            if (entity != structure)
            {
                voxel = null;
                transform = default;
                return false;
            }

            voxel = voxelObject;
            transform = new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One);
            return true;
        }
    }

    private sealed class StubPhysics : IPhysics
    {
        public event EventHandler<EventArgs>? FixedUpdate;
        public RaycastResult Raycast(in Ray ray) => default;
        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class StubContent : IInteractionContent
    {
        public bool TryGetPlaceable(string? itemID, out PlaceableBrick placeable)
        {
            placeable = new PlaceableBrick(BRICK_DATA_ID, WaywardBeyond.Client.Core.Bricks.BrickShape.Block, shapeable: false, hasOrientableTag: false, brightness: 0);
            return true;
        }

        public bool TryGetLoot(ushort brickDataID, out ItemData loot)
        {
            loot = new ItemData { ID = "rock", Count = 1, MaxSize = 100 };
            return true;
        }
    }
}