using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     Shared interaction validation for client prediction and server authority.
///     A hintless or invalid hint resolves to <see cref="InteractionAction.None"/>.
/// </summary>
public static class InteractionResolver
{
    private const float SURFACE_EPSILON = 0.001f;
    private const float PLACEMENT_PENETRATION_EPSILON = 0.005f;
    
    private static readonly Vector3 _halfCell = new(0.5f);

    /// <summary>
    /// Validates the hinted structure + cell against the world and resolves the action. Never throws;
    /// a hint-less or invalid hint yields <see cref="InteractionAction.None"/>.
    /// </summary>
    public static InteractionResolution Resolve(
        in InteractionRequest request,
        in IVoxelInteractionWorld world,
        IBrickRegistry brickRegistry
    ) {
        if (request.Hint == null)
        {
            return InteractionResolution.None;
        }

        //  Brick edits ride the primary edge. A held placeable makes the press a place; without one the
        //  press breaks the targeted occupied cell.
        if (request.Kind != InteractionKind.PrimaryPressed)
        {
            return InteractionResolution.None;
        }

        bool isPlace = request.Placeable != null;

        BrickInteraction hint = request.Hint.Value;

        //  Resolve the hinted structure by its stable identity. It must carry a voxel container and
        //  transform for reach + occupancy validation.
        Result<VoxelTarget> targetResult = world.GetTarget(Uuid.FromValue(hint.TargetEntity));
        if (!targetResult.Success || targetResult.Value.VoxelObject == null)
        {
            return InteractionResolution.None;
        }

        VoxelTarget target = targetResult.Value;
        VoxelObject voxelObject = target.VoxelObject;
        TransformComponent transform = target.Transform;
        int entity = target.Entity;

        Int3 hintCell = new(hint.TargetX, hint.TargetY, hint.TargetZ);

        //  Reach: the brick is in reach when any part of its cell is within reach of the interaction
        //  origin. Measuring to the nearest point of the cell matches client targeting, which hits a
        //  face rather than the center.
        if (!IsReachable(hintCell, request.Origin, in transform, request.Reach))
        {
            return InteractionResolution.None;
        }

        Voxel hintVoxel = voxelObject.Get(hintCell.X, hintCell.Y, hintCell.Z);

        if (!isPlace)
        {
            //  Break requires an occupied cell.
            if (hintVoxel.ID == 0)
            {
                return InteractionResolution.None;
            }

            return new InteractionResolution(InteractionAction.Break, entity, hintCell, hintVoxel);
        }

        //  Place requires an empty destination cell and a held placeable brick.
        if (hintVoxel.ID != 0 || request.Placeable == null)
        {
            return InteractionResolution.None;
        }

        //  The hint carries a raw shape byte. Reject a value outside the enum before it reaches the
        //  voxel, because ShapeLight keeps only four bits.
        if (hint.HintShape > (byte)BrickShape.Any)
        {
            return InteractionResolution.None;
        }

        Brick placeable = request.Placeable;

        //  An unregistered brick resolves to voxel id 0, which would consume the held item for an
        //  empty write. Reject the place instead.
        if (brickRegistry.Id(placeable.ID) == 0)
        {
            return InteractionResolution.None;
        }

        Voxel voxel = placeable.ToVoxel((BrickShape)hint.HintShape, brickRegistry, new Orientation(hint.HintOrientation));
        BrickShape resolvedShape = voxel.GetShapeLight().Shape;
        Orientation resolvedOrientation = voxel.GetOrientation();
        if (world.IntersectsCollider(in target, in hintCell, placeable, resolvedShape, resolvedOrientation))
        {
            return InteractionResolution.None;
        }

        return new InteractionResolution(InteractionAction.Place, entity, hintCell, voxel);
    }

    /// <summary>
    /// Returns whether placing <paramref name="placeable"/> at <paramref name="coordinate"/> on
    /// <paramref name="targetEntity"/> intersects any physics collider other than the target structure itself.
    /// </summary>
    public static bool IntersectsCollider(
        IPhysics physics,
        int targetEntity,
        in TransformComponent targetTransform,
        in Int3 coordinate,
        Brick placeable,
        BrickShape shape,
        Orientation orientation
    ) {
        ReadOnlySpan<BrickColliderShape> parts = BrickColliders.ForShape(shape, placeable);
        if (parts.IsEmpty)
        {
            return false;
        }

        bool isPlainCube = BrickColliders.IsFullBlock(shape, placeable);
        Quaternion voxelOrientation = isPlainCube
            ? Quaternion.Identity
            : orientation.ToQuaternion();

        var localCenter = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);

        for (var i = 0; i < parts.Length; i++)
        {
            BrickColliderShape part = parts[i];
            Vector3 localPosition = part.Offset == Vector3.Zero
                ? localCenter
                : localCenter + Vector3.Transform(part.Offset, voxelOrientation);
            Quaternion localRotation = part.Orientation == Quaternion.Identity
                ? voxelOrientation
                : voxelOrientation * part.Orientation;

            Vector3 worldPosition = Vector3.Transform(localPosition * targetTransform.Scale, targetTransform.Orientation)
                + targetTransform.Position;
            Quaternion worldRotation = targetTransform.Orientation * localRotation;

            var query = new ShapeOverlapQuery(
                part.Shape,
                worldPosition,
                worldRotation,
                targetTransform.Scale,
                ignoredEntity: targetEntity,
                minPenetration: PLACEMENT_PENETRATION_EPSILON
            );

            if (physics.Overlaps(in query))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any part of a brick cell is within <paramref name="reach"/> of a world position.</summary>
    private static bool IsReachable(in Int3 coordinate, Vector3 origin, in TransformComponent transform, float reach)
    {
        //  The cell is axis-aligned in brick space, so clamp the origin there instead of transforming
        //  every cell corner into world space.
        Vector3 localOrigin = Vector3.Transform(origin - transform.Position, Quaternion.Inverse(transform.Orientation));
        var center = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);
        Vector3 nearest = Vector3.Clamp(localOrigin, center - _halfCell, center + _halfCell);
        return Vector3.DistanceSquared(localOrigin, nearest) <= reach * reach;
    }

    /// <summary>Maps a world position into the structure's brick-space coordinate.</summary>
    public static Int3 WorldToBrickSpace(Vector3 position, Vector3 origin, Quaternion orientation)
    {
        Vector3 localPos = Vector3.Transform(position - origin, Quaternion.Inverse(orientation)) + new Vector3(0.5f);

        //  Voxels are centered on integer coordinates, so a voxel at c spans [c - 0.5, c + 0.5). The
        //  +0.5 above shifts the sample so the floor lands on the containing cell.
        var x = (int)Math.Floor(localPos.X);
        var y = (int)Math.Floor(localPos.Y);
        var z = (int)Math.Floor(localPos.Z);

        return new Int3(x, y, z);
    }

    /// <summary>Maps a brick-space coordinate to the world position of its voxel center.</summary>
    public static Vector3 BrickToWorldSpace(Int3 coordinate, Vector3 origin, Quaternion orientation)
    {
        var localPosition = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);
        return Vector3.Transform(localPosition, orientation) + origin;
    }

    /// <summary>
    /// Resolves the hit voxel coordinate from a physics raycast hit point and surface normal.
    /// Uses a small inward nudge so thin and sloped hitboxes stay inside the hit cell.
    /// </summary>
    public static Int3 ResolveHitCell(
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 structureOrigin,
        Quaternion structureOrientation
    ) {
        Vector3 inwardPoint = hitPoint - hitNormal * SURFACE_EPSILON;
        return WorldToBrickSpace(inwardPoint, structureOrigin, structureOrientation);
    }

    /// <summary>
    /// Resolves the direct adjacent placement voxel coordinate from a physics raycast hit point and surface normal.
    /// Never leaps across occupied cells; outer faces step directly into the neighbor and recessed or sloped
    /// faces step along the dominant local normal axis from the hit cell.
    /// </summary>
    public static Int3 ResolvePlacementCell(
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 structureOrigin,
        Quaternion structureOrientation
    ) {
        Int3 hitCell = ResolveHitCell(hitPoint, hitNormal, structureOrigin, structureOrientation);

        //  Outer cell faces step straight into the adjacent cell.
        Vector3 outwardPoint = hitPoint + hitNormal * SURFACE_EPSILON;
        Int3 outwardCell = WorldToBrickSpace(outwardPoint, structureOrigin, structureOrientation);
        if (outwardCell != hitCell)
        {
            return outwardCell;
        }

        //  Recessed or sloped faces (slabs, plates, columns, stairs, slopes, custom shapes) stay inside
        //  the hit cell after a tiny nudge. Step along the dominant local normal axis from the hit cell.
        Quaternion inverseOrientation = Quaternion.Inverse(structureOrientation);
        Vector3 localNormal = Vector3.Transform(hitNormal, inverseOrientation);
        Vector3 localHit = Vector3.Transform(hitPoint - structureOrigin, inverseOrientation);
        Vector3 cellOffset = localHit - new Vector3(hitCell.X, hitCell.Y, hitCell.Z);

        Int3 primaryStep = PickCardinalStep(localNormal, cellOffset);
        return new Int3(hitCell.X + primaryStep.X, hitCell.Y + primaryStep.Y, hitCell.Z + primaryStep.Z);
    }

    private static Int3 PickCardinalStep(Vector3 localNormal, Vector3 cellOffset)
    {
        const float minAxisComponent = 0.01f;
        const float tieTolerance = 0.05f;

        var chosenAxis = -1;
        var bestMagnitude = 0f;
        var bestTieBreak = float.NegativeInfinity;
        var bestSign = 0;

        for (var axis = 0; axis < 3; axis++)
        {
            float component = axis == 0 ? localNormal.X : (axis == 1 ? localNormal.Y : localNormal.Z);
            float magnitude = MathF.Abs(component);
            if (magnitude < minAxisComponent)
            {
                continue;
            }

            int sign = component >= 0f ? 1 : -1;
            float offsetAlongAxis = (axis == 0 ? cellOffset.X : (axis == 1 ? cellOffset.Y : cellOffset.Z)) * sign;

            bool isBetter = chosenAxis < 0
                || magnitude > bestMagnitude + tieTolerance
                || (MathF.Abs(magnitude - bestMagnitude) <= tieTolerance && offsetAlongAxis > bestTieBreak);

            if (!isBetter)
            {
                continue;
            }

            chosenAxis = axis;
            bestMagnitude = magnitude;
            bestTieBreak = offsetAlongAxis;
            bestSign = sign;
        }

        return chosenAxis switch
        {
            0 => new Int3(bestSign, 0, 0),
            1 => new Int3(0, bestSign, 0),
            2 => new Int3(0, 0, bestSign),
            _ => default,
        };
    }
}