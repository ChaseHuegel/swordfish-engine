#nullable enable
using System;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins collider-overlap rejection during brick placement against a live Jolt physics world:
/// players, NPCs/monsters, other ships, shaped bricks, and rotated structures.
/// </summary>
public class BrickPlacementCollisionTests
{
    private static readonly Uuid STRUCTURE_ID = Uuid.FromValue(10);

    [Fact]
    public void RejectsPlacingABrickInsideThePlayersOwnCollider()
    {
        var (physics, store, structureEntity, voxels) = CreateStructureWorld();
        AddPlayer(store, new Vector3(0f, 1.2f, 0f));
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var world = new ServerVoxelInteractionWorld(store, physics);
        var request = PlaceRequest(new Vector3(0f, 1.2f, 0f), new Int3(0, 1, 0), BrickShape.Block);

        InteractionResolution resolution = InteractionResolver.Resolve(in request, world, new FakeRegistry());

        Assert.Equal(InteractionAction.None, resolution.Action);
        Assert.True(structureEntity >= 0 && voxels.Get(0, 0, 0).ID != 0);
    }

    [Fact]
    public void AllowsPlacingABrickAdjacentToThePlayerAndExistingStructureBricks()
    {
        var (physics, store, _, _) = CreateStructureWorld();
        AddPlayer(store, new Vector3(0f, 1.2f, 0f));
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var world = new ServerVoxelInteractionWorld(store, physics);
        //  (1, 0, 0) touches the existing floor brick at (0, 0, 0) flush along X = 0.5 and does not intersect the player at (0, 1.2, 0).
        var request = PlaceRequest(new Vector3(0f, 1.2f, 0f), new Int3(1, 0, 0), BrickShape.Block);

        InteractionResolution resolution = InteractionResolver.Resolve(in request, world, new FakeRegistry());

        Assert.Equal(InteractionAction.Place, resolution.Action);
        Assert.Equal(new Int3(1, 0, 0), resolution.Coordinate);
    }

    [Fact]
    public void RejectsPlacingABrickThatIntersectsAnotherEntityCollider()
    {
        var (physics, store, _, _) = CreateStructureWorld();

        //  NPC / monster capsule standing in cell (2, 1, 0).
        int npc = store.Alloc(Uuid.FromValue(20));
        store.AddOrUpdate(npc, new TransformComponent(new Vector3(2f, 1f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(npc, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(npc, new ColliderComponent(new Capsule(height: 1.0f, radius: 0.35f)));

        //  Second ship structure occupying (4, 0, 0) in world space.
        var shipVoxels = new VoxelObject(4);
        shipVoxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, lightLevel: 0), default));
        int otherShip = store.Alloc(Uuid.FromValue(30));
        store.AddOrUpdate(otherShip, new TransformComponent(new Vector3(4f, 0f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(otherShip, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(otherShip, new VoxelWorldComponent(shipVoxels));
        store.AddOrUpdate(otherShip, new ColliderComponent(VoxelColliderBuilder.BuildCollision(shipVoxels)));

        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var world = new ServerVoxelInteractionWorld(store, physics);
        var intoNpc = PlaceRequest(Vector3.Zero, new Int3(2, 1, 0), BrickShape.Block);
        var intoOtherShip = PlaceRequest(Vector3.Zero, new Int3(4, 0, 0), BrickShape.Block);
        var clearCell = PlaceRequest(Vector3.Zero, new Int3(3, 0, 0), BrickShape.Block);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in intoNpc, world, new FakeRegistry()).Action);
        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in intoOtherShip, world, new FakeRegistry()).Action);
        Assert.Equal(InteractionAction.Place, InteractionResolver.Resolve(in clearCell, world, new FakeRegistry()).Action);
    }

    [Fact]
    public void ShapedBrickChecksActualBrickShapeInsteadOfFullCell()
    {
        var (physics, store, _, _) = CreateStructureWorld();

        //  Small collider in the upper half (+Y) of cell (1, 1, 0), clear of a bottom slab in [-0.5, 0].
        int drone = store.Alloc(Uuid.FromValue(40));
        store.AddOrUpdate(drone, new TransformComponent(new Vector3(1f, 1.3f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(drone, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(drone, new ColliderComponent(new Sphere(0.15f)));
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var world = new ServerVoxelInteractionWorld(store, physics);
        var fullBlock = PlaceRequest(Vector3.Zero, new Int3(1, 1, 0), BrickShape.Block);
        var bottomSlab = PlaceRequest(Vector3.Zero, new Int3(1, 1, 0), BrickShape.Slab);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in fullBlock, world, new FakeRegistry()).Action);
        Assert.Equal(InteractionAction.Place, InteractionResolver.Resolve(in bottomSlab, world, new FakeRegistry()).Action);
    }

    [Fact]
    public void RotatedStructureTransformsPlacementCellIntoWorldSpace()
    {
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        var (physics, store, _, _) = CreateStructureWorld(structureOrientation: rotation);

        //  Local cell (1, 0, 0) rotated +90 deg around Y lands at world (0, 0, -1).
        AddPlayer(store, new Vector3(0f, 0.2f, -1f));
        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        var world = new ServerVoxelInteractionWorld(store, physics);
        var blocked = PlaceRequest(Vector3.Zero, new Int3(1, 0, 0), BrickShape.Block);
        var clear = PlaceRequest(Vector3.Zero, new Int3(-1, 0, 0), BrickShape.Block);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in blocked, world, new FakeRegistry()).Action);
        Assert.Equal(InteractionAction.Place, InteractionResolver.Resolve(in clear, world, new FakeRegistry()).Action);
    }

    private static (JoltPhysicsSystem Physics, DataStore Store, int StructureEntity, VoxelObject Voxels) CreateStructureWorld(
        Quaternion? structureOrientation = null
    ) {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();

        var voxels = new VoxelObject(4);
        voxels.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, lightLevel: 0), default));

        int structure = store.Alloc(STRUCTURE_ID);
        store.AddOrUpdate(structure, new TransformComponent(Vector3.Zero, structureOrientation ?? Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(structure, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(structure, new VoxelWorldComponent(voxels));
        store.AddOrUpdate(structure, new ColliderComponent(VoxelColliderBuilder.BuildCollision(voxels)));

        return (physics, store, structure, voxels);
    }

    private static int AddPlayer(DataStore store, Vector3 position)
    {
        int player = store.Alloc(Uuid.FromValue(1));
        store.AddOrUpdate(player, new TransformComponent(position, Quaternion.Identity, CharacterDefaults.PlayerScale));
        store.AddOrUpdate(player, CharacterDefaults.CreateCharacterPhysicsComponent());
        store.AddOrUpdate(player, CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale));
        return player;
    }

    private static InteractionRequest PlaceRequest(Vector3 origin, Int3 cell, BrickShape shape)
    {
        var hint = new BrickInteraction
        {
            TargetEntity = STRUCTURE_ID.ToValue(),
            TargetX = cell.X,
            TargetY = cell.Y,
            TargetZ = cell.Z,
            HintShape = (byte)shape,
            HintOrientation = 0,
        };

        return new InteractionRequest(
            origin,
            hint,
            InteractionKind.SecondaryPressed,
            Placeable(),
            GameMode.Creative,
            CharacterDefaults.DEFAULT_REACH
        );
    }

    private static Brick Placeable()
    {
        return new Brick(
            "wb:panel",
            alpha: BrickAlpha.Opaque,
            passable: false,
            meshID: null,
            shape: BrickShape.Any,
            textures: default,
            tags: null
        );
    }

    private sealed class FakeRegistry : IBrickRegistry
    {
        public ushort Id(string name) => name == "wb:panel" ? (ushort)1 : (ushort)0;

        public string? Name(ushort id) => id == 1 ? "wb:panel" : null;
    }
}
