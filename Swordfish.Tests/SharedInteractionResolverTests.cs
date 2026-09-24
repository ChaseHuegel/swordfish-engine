using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// 4.1 acceptance: the shared interaction resolver validates a client hint (structure identity + cell)
/// purely against the world - reach (squared), occupancy, and held-placeable - with no raycasting, so
/// prediction and authority resolve identically from the same hint. A hint-less, unknown-structure, or
/// out-of-reach interaction resolves to <see cref="InteractionAction.None"/>.
/// </summary>
public class SharedInteractionResolverTests
{
    private const ulong STRUCTURE_UUID = 0xBEEF;
    private const int BREAK_BRICK_ID = 3;
    private const int PLACE_BRICK_ID = 7;

    private static readonly TransformComponent _identityTransform = new(Vector3.Zero, Quaternion.Identity, Vector3.One);
    private static readonly Vector3 _origin = new(-1f, 0f, 0f);
    private static readonly PlaceableBrick _placeableBrick = new(
        dataID: PLACE_BRICK_ID,
        shape: BrickShape.Block,
        shapeable: false,
        hasOrientableTag: false,
        brightness: 0
    );

    [Fact]
    public void HintLessInteractionResolvesToNone()
    {
        (IVoxelInteractionWorld world, _) = BuildWorld();

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, hint: null, InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void UnknownStructureResolvesToNone()
    {
        (IVoxelInteractionWorld world, _) = BuildWorld();
        var hint = Hint(0xDEAD, 0, 0, 0);

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, hint, InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void BreakResolvesToOccupiedCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.Break, result.Action);
        Assert.Equal(new Int3(0, 0, 0), result.Coordinate);
        Assert.Equal(BREAK_BRICK_ID, result.Voxel.ID);
    }

    [Fact]
    public void BreakRejectsEmptyTargetCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        //  Cell (1,0,0) is empty; break requires an occupied cell.
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 1, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void ResolvesHintCellWithinReach()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        voxelObject.Set(1, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 1, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.Break, result.Action);
        Assert.Equal(new Int3(1, 0, 0), result.Coordinate);
    }

    [Fact]
    public void RejectsHintCellBeyondReach()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        //  The hint claims a cell far outside reach; the server must reject it (prevents
        //  teleport-breaking/placing far from the player).
        voxelObject.Set(500, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 500, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void PlaceResolvesToEmptyDestinationCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 1, 0, 0), InteractionKind.SecondaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.Place, result.Action);
        Assert.Equal(new Int3(1, 0, 0), result.Coordinate);
        Assert.Equal(PLACE_BRICK_ID, result.Voxel.ID);
    }

    [Fact]
    public void PlaceRejectsOccupiedDestination()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        voxelObject.Set(1, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 1, 0, 0), InteractionKind.SecondaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void PlaceRejectsMissingPlaceableItem()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution result = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 1, 0, 0), InteractionKind.SecondaryPressed, placeable: null, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void ResolutionIsDeterministicForIdenticalInputs()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld();
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));

        InteractionResolution first = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);
        InteractionResolution second = SharedInteractionResolver.Resolve(_origin, Hint(STRUCTURE_UUID, 0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(first.Action, second.Action);
        Assert.Equal(first.Coordinate, second.Coordinate);
        Assert.Equal(first.Voxel, second.Voxel);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, -2, 3)]
    [InlineData(-4, 5, -6)]
    [InlineData(16, 0, -1)]
    public void BrickToWorldAndWorldToBrickAreInverseAboutCellCenters(int x, int y, int z)
    {
        var coordinate = new Int3(x, y, z);
        var origin = new Vector3(2f, -1f, 0.5f);
        var orientation = Quaternion.CreateFromYawPitchRoll(0.5f, -0.3f, 0.2f);

        Vector3 center = SharedInteractionResolver.BrickToWorldSpace(coordinate, origin, orientation);
        var expectedCenter = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
        Assert.Equal(Vector3.Transform(expectedCenter, orientation) + origin, center);

        Assert.Equal(coordinate, SharedInteractionResolver.WorldToBrickSpace(center, origin, orientation));
    }

    private static BrickInteraction Hint(ulong targetEntity, int x, int y, int z)
    {
        return new BrickInteraction
        {
            TargetEntity = targetEntity,
            TargetX = x,
            TargetY = y,
            TargetZ = z,
            HintShape = (byte)BrickShape.Block,
            HintOrientation = 0,
        };
    }

    private static (IVoxelInteractionWorld world, VoxelObject voxelObject) BuildWorld()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        var world = new DeterministicWorld(STRUCTURE_UUID, voxelObject, _identityTransform);
        return (world, voxelObject);
    }

    /// <summary>
    /// Returns a single structure the resolver resolves by its stable identity, so the ray-free validation
    /// math runs deterministically headlessly. Both "sides" of a parity test share this same world.
    /// </summary>
    private sealed class DeterministicWorld : IVoxelInteractionWorld
    {
        private readonly ulong _structureUuid;
        private readonly VoxelObject _voxelObject;
        private readonly TransformComponent _transform;

        public DeterministicWorld(ulong structureUuid, VoxelObject voxelObject, TransformComponent transform)
        {
            _structureUuid = structureUuid;
            _voxelObject = voxelObject;
            _transform = transform;
        }

        public bool TryRaycast(in Ray ray, out RaycastResult result)
        {
            result = default;
            return false;
        }

        public bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            if (entity != 0)
            {
                voxelObject = null;
                transform = default;
                return false;
            }

            voxelObject = _voxelObject;
            transform = _transform;
            return true;
        }

        public bool TryGetVoxelTarget(in Uuid entityUuid, out int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            if (entityUuid != Uuid.FromValue(_structureUuid))
            {
                entity = default;
                voxelObject = null;
                transform = default;
                return false;
            }

            entity = 0;
            voxelObject = _voxelObject;
            transform = _transform;
            return true;
        }
    }
}