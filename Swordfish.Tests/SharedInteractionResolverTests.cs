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
/// 4.1 acceptance: the shared interaction resolver maps identical (ray, hint, item, mode) inputs to
/// identical actions on both prediction and authority (it is one shared function), and a hint-less or
/// empty-target interaction resolves to <see cref="InteractionAction.None"/> - it never throws or assumes
/// a hint is present. The world is a deterministic fake; resolver parity is by construction, so these
/// tests pin the resolution rules and the ported targeting (surface bias, march-back).
/// </summary>
public class SharedInteractionResolverTests
{
    private const int STRUCTURE_ENTITY = 5;
    private const int BREAK_BRICK_ID = 3;
    private const int PLACE_BRICK_ID = 7;

    private static readonly TransformComponent _identityTransform = new(Vector3.Zero, Quaternion.Identity, Vector3.One);
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
        (IVoxelInteractionWorld world, _) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, hint: null, InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void BreakResolvesToOccupiedCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.Break, result.Action);
        Assert.Equal(STRUCTURE_ENTITY, result.Entity);
        Assert.Equal(new Int3(0, 0, 0), result.Coordinate);
        Assert.Equal(BREAK_BRICK_ID, result.Voxel.ID);
    }

    [Fact]
    public void BreakRejectsEmptyTargetCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(1.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        //  The ray resolves cell (1,0,0) which is empty; break requires an occupied cell.
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(1, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void ServerAcceptsValidHintCellAwayFromServerRayCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        //  The client aims at (1,0,0) through its camera ray; the server's (different) body ray happens
        //  to land on (0,0,0). Both cells are on the same structure; the hint cell is occupied, so the
        //  server must accept the break rather than reject on ray-parity.
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        voxelObject.Set(1, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(1, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.Break, result.Action);
        Assert.Equal(new Int3(1, 0, 0), result.Coordinate);
        Assert.Equal(BREAK_BRICK_ID, result.Voxel.ID);
    }

    [Fact]
    public void ServerRejectsHintCellBeyondReach()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(500, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        //  The hint claims a cell far outside reach even though the ray hits the structure nearby; the
        //  server must reject it (prevents teleport-breaking far from the player).
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(500, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void PlaceResolvesToEmptyDestinationCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        //  Placement biases toward the adjacent destination cell (1,0,0), which is empty.
        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(1, 0, 0), InteractionKind.SecondaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.Place, result.Action);
        Assert.Equal(STRUCTURE_ENTITY, result.Entity);
        Assert.Equal(new Int3(1, 0, 0), result.Coordinate);
        Assert.Equal(PLACE_BRICK_ID, result.Voxel.ID);
    }

    [Fact]
    public void PlaceRejectsOccupiedDestination()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(1.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(1, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(1, 0, 0), InteractionKind.SecondaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void PlaceRejectsMissingPlaceableItem()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(1, 0, 0), InteractionKind.SecondaryPressed, placeable: null, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void PlaceRejectsWhenMarchBackFindsNoEmptyCell()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        //  A solid wall: placing against (0,0,0) marches back but every candidate cell stays occupied.
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        voxelObject.Set(1, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        voxelObject.Set(-1, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(1, 0, 0), InteractionKind.SecondaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void ReachBeyondLimitResolvesToNone()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-100f, 0f, 0f), Vector3.UnitX);

        InteractionResolution result = SharedInteractionResolver.Resolve(ray, Hint(0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, reach: SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(InteractionAction.None, result.Action);
    }

    [Fact]
    public void ResolutionIsDeterministicForIdenticalInputs()
    {
        (IVoxelInteractionWorld world, VoxelObject voxelObject) = BuildWorld(hitPoint: new Vector3(0.5f, 0f, 0f), normal: Vector3.UnitX);
        voxelObject.Set(0, 0, 0, new Voxel(BREAK_BRICK_ID, 0, 0));
        var ray = new Ray(new Vector3(-1f, 0f, 0f), Vector3.UnitX);

        InteractionResolution first = SharedInteractionResolver.Resolve(ray, Hint(0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);
        InteractionResolution second = SharedInteractionResolver.Resolve(ray, Hint(0, 0, 0), InteractionKind.PrimaryPressed, _placeableBrick, GameMode.Creative, SharedInteractionResolver.DEFAULT_REACH, world);

        Assert.Equal(first.Action, second.Action);
        Assert.Equal(first.Coordinate, second.Coordinate);
        Assert.Equal(first.Voxel, second.Voxel);
    }

    private static BrickInteraction Hint(int x, int y, int z)
    {
        return new BrickInteraction
        {
            TargetX = x,
            TargetY = y,
            TargetZ = z,
            HintShape = (byte)BrickShape.Block,
            HintOrientation = 0,
        };
    }

    private static (IVoxelInteractionWorld world, VoxelObject voxelObject) BuildWorld(Vector3 hitPoint, Vector3 normal)
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        var world = new DeterministicWorld(STRUCTURE_ENTITY, voxelObject, _identityTransform, hitPoint, normal);
        return (world, voxelObject);
    }

    /// <summary>
    /// Returns a canned hit on a single structure for any ray, so the resolver's targeting math runs
    /// deterministically headlessly. Both "sides" of a parity test would share this same world.
    /// </summary>
    private sealed class DeterministicWorld : IVoxelInteractionWorld
    {
        private readonly int _entity;
        private readonly VoxelObject _voxelObject;
        private readonly TransformComponent _transform;
        private readonly Vector3 _hitPoint;
        private readonly Vector3 _normal;

        public DeterministicWorld(int entity, VoxelObject voxelObject, TransformComponent transform, Vector3 hitPoint, Vector3 normal)
        {
            _entity = entity;
            _voxelObject = voxelObject;
            _transform = transform;
            _hitPoint = hitPoint;
            _normal = normal;
        }

        public bool TryRaycast(in Ray ray, out RaycastResult result)
        {
            result = new RaycastResult(true, new Entity(_entity, new DataStore()), _hitPoint, _normal);
            return true;
        }

        public bool TryGetVoxelTarget(int entity, out VoxelObject? voxelObject, out TransformComponent transform)
        {
            if (entity != _entity)
            {
                voxelObject = null;
                transform = default;
                return false;
            }

            voxelObject = _voxelObject;
            transform = _transform;
            return true;
        }
    }
}