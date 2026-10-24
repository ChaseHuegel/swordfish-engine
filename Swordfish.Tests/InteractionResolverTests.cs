#nullable enable
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Xunit;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Voxels;

namespace Swordfish.Tests;

/// <summary>
/// Pins the shared interaction resolver that client prediction and server authority both run. A hint-less
/// or invalid hint must resolve to <see cref="InteractionAction.None"/> and never throw.
/// </summary>
public class InteractionResolverTests
{
    private static readonly Uuid TARGET = Uuid.FromValue(1);

    [Fact]
    public void ResolvesABreakOnAnOccupiedCell()
    {
        VoxelObject voxels = FilledVoxels();
        var request = Break(cell: 0, reach: 9.5f);

        InteractionResolution resolution = InteractionResolver.Resolve(in request, World(voxels), Registry());

        Assert.Equal(InteractionAction.Break, resolution.Action);
        Assert.Equal(0, resolution.Entity);
        Assert.Equal(1, resolution.Voxel.ID);
    }

    [Fact]
    public void RejectsABreakOnAnEmptyCell()
    {
        var voxels = new VoxelObject(4);
        var request = Break(cell: 0, reach: 9.5f);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, World(voxels), Registry()).Action);
    }

    [Fact]
    public void RejectsAHintlessRequest()
    {
        var voxels = new VoxelObject(4);
        var request = new InteractionRequest(Vector3.Zero, null, InteractionKind.PrimaryPressed, null, GameMode.Creative, 9.5f);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, World(voxels), Registry()).Action);
    }

    [Fact]
    public void RejectsAReleaseEdge()
    {
        VoxelObject voxels = FilledVoxels();
        var released = new InteractionRequest(
            Vector3.Zero,
            Hint(cell: 0),
            InteractionKind.PrimaryReleased,
            null,
            GameMode.Creative,
            9.5f
        );

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in released, World(voxels), Registry()).Action);
    }

    [Fact]
    public void ResolvesAPlaceOnAnEmptyCellWithTheRequestedShape()
    {
        var voxels = new VoxelObject(4);
        var request = Place(cell: 0, reach: 9.5f, shape: (byte)BrickShape.Plate, brick: Placeable());

        InteractionResolution resolution = InteractionResolver.Resolve(in request, World(voxels), Registry());

        Assert.Equal(InteractionAction.Place, resolution.Action);
        Assert.Equal(1, resolution.Voxel.ID);
        Assert.Equal(BrickShape.Plate, resolution.Voxel.GetShapeLight().Shape);
    }

    [Fact]
    public void RejectsAPlaceOnAnOccupiedCell()
    {
        VoxelObject voxels = FilledVoxels();
        var request = Place(cell: 0, reach: 9.5f, shape: (byte)BrickShape.Block, brick: Placeable());

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, World(voxels), Registry()).Action);
    }

    [Fact]
    public void RejectsAPlaceWithoutAHeldPlaceable()
    {
        var voxels = new VoxelObject(4);
        var request = Place(cell: 0, reach: 9.5f, shape: (byte)BrickShape.Block, brick: null);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, World(voxels), Registry()).Action);
    }

    [Fact]
    public void RejectsAnOutOfRangeHintShape()
    {
        var voxels = new VoxelObject(4);
        var request = Place(cell: 0, reach: 9.5f, shape: (byte)(BrickShape.Any + 1), brick: Placeable());

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, World(voxels), Registry()).Action);
    }

    [Fact]
    public void RejectsAPlaceableBrickMissingFromTheRegistry()
    {
        var voxels = new VoxelObject(4);
        Brick unknown = Placeable("wb:unknown");
        var request = Place(cell: 0, reach: 9.5f, shape: (byte)BrickShape.Block, brick: unknown);

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, World(voxels), Registry()).Action);
    }

    [Fact]
    public void ReachMeasuresToTheNearestCellPoint()
    {
        VoxelObject voxels = FilledVoxels();

        //  The cell center is 10 m away on X. Its nearest face is 9.5 m away.
        var reaching = new InteractionRequest(
            Vector3.Zero,
            Hint(cell: 10),
            InteractionKind.PrimaryPressed,
            null,
            GameMode.Creative,
            9.5f
        );

        var outOfReach = new InteractionRequest(
            Vector3.Zero,
            Hint(cell: 10),
            InteractionKind.PrimaryPressed,
            null,
            GameMode.Creative,
            9.4f
        );

        Assert.Equal(InteractionAction.Break, InteractionResolver.Resolve(in reaching, World(voxels), Registry()).Action);
        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in outOfReach, World(voxels), Registry()).Action);
    }

    [Fact]
    public void RejectsAnUnknownTargetEntity()
    {
        VoxelObject voxels = FilledVoxels();
        var request = Break(cell: 0, reach: 9.5f);

        var otherWorld = new FakeWorld(Uuid.FromValue(99), new VoxelTarget(0, voxels, Transform()));

        Assert.Equal(InteractionAction.None, InteractionResolver.Resolve(in request, otherWorld, Registry()).Action);
    }

    private static InteractionRequest Break(int cell, float reach)
    {
        return new InteractionRequest(
            Vector3.Zero,
            Hint(cell),
            InteractionKind.PrimaryPressed,
            null,
            GameMode.Creative,
            reach
        );
    }

    private static InteractionRequest Place(int cell, float reach, byte shape, Brick? brick)
    {
        return new InteractionRequest(
            Vector3.Zero,
            Hint(cell, shape),
            InteractionKind.SecondaryPressed,
            brick,
            GameMode.Creative,
            reach
        );
    }

    private static BrickInteraction Hint(int cell, byte shape = 0)
    {
        return new BrickInteraction
        {
            TargetEntity = TARGET.ToValue(),
            TargetX = cell,
            TargetY = 0,
            TargetZ = 0,
            HintShape = shape,
            HintOrientation = 0,
        };
    }

    private static VoxelObject FilledVoxels()
    {
        var voxels = new VoxelObject(4);
        for (var cell = 0; cell <= 10; cell++)
        {
            voxels.Set(cell, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, lightLevel: 0), default));
        }

        return voxels;
    }

    private static TransformComponent Transform()
    {
        return new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One);
    }

    private static FakeWorld World(VoxelObject voxels)
    {
        return new FakeWorld(TARGET, new VoxelTarget(0, voxels, Transform()));
    }

    private static Brick Placeable(string id = "wb:panel")
    {
        return new Brick(
            id,
            transparent: false,
            passable: false,
            meshID: null,
            shape: BrickShape.Any,
            textures: default,
            tags: null
        );
    }

    private static IBrickRegistry Registry()
    {
        return new FakeRegistry();
    }

    private sealed class FakeWorld(in Uuid target, in VoxelTarget value) : IVoxelInteractionWorld
    {
        private readonly Uuid _target = target;
        private readonly VoxelTarget _value = value;

        public Result<VoxelTarget> GetTarget(in Uuid entityUuid)
        {
            return entityUuid == _target
                ? Result<VoxelTarget>.FromSuccess(_value)
                : Result<VoxelTarget>.FromFailure((string?)null);
        }
    }

    private sealed class FakeRegistry : IBrickRegistry
    {
        public ushort Id(string name)
        {
            return name == "wb:panel" ? (ushort)1 : (ushort)0;
        }

        public string? Name(ushort id)
        {
            return id == 1 ? "wb:panel" : null;
        }
    }
}
