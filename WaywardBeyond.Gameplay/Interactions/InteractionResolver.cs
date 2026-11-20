using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
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
    private static readonly Vector3 HALF_CELL = new(0.5f);

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

        bool isBreak = request.Kind == InteractionKind.PrimaryPressed;
        bool isPlace = request.Kind == InteractionKind.SecondaryPressed;
        if (!isBreak && !isPlace)
        {
            return InteractionResolution.None;
        }

        BrickInteraction hint = request.Hint.Value;

        //  Resolve the hinted structure by its stable identity. It must carry a voxel container and
        //  transform for reach + occupancy validation.
        Result<VoxelTarget> targetResult = world.GetTarget(Uuid.FromValue(hint.TargetEntity));
        if (!targetResult.Success || targetResult.Value.VoxelObject == null)
        {
            return InteractionResolution.None;
        }

        VoxelObject voxelObject = targetResult.Value.VoxelObject;
        TransformComponent transform = targetResult.Value.Transform;
        int entity = targetResult.Value.Entity;

        Int3 hintCell = new(hint.TargetX, hint.TargetY, hint.TargetZ);

        //  Reach: the brick is in reach when any part of its cell is within reach of the interaction
        //  origin. Measuring to the nearest point of the cell matches client targeting, which hits a
        //  face rather than the center.
        if (!IsReachable(hintCell, request.Origin, in transform, request.Reach))
        {
            return InteractionResolution.None;
        }

        Voxel hintVoxel = voxelObject.Get(hintCell.X, hintCell.Y, hintCell.Z);

        if (isBreak)
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
        return new InteractionResolution(InteractionAction.Place, entity, hintCell, voxel);
    }

    /// <summary>Returns whether any part of a brick cell is within <paramref name="reach"/> of a world position.</summary>
    private static bool IsReachable(in Int3 coordinate, Vector3 origin, in TransformComponent transform, float reach)
    {
        //  The cell is axis-aligned in brick space, so clamp the origin there instead of transforming
        //  every cell corner into world space.
        Vector3 localOrigin = Vector3.Transform(origin - transform.Position, Quaternion.Inverse(transform.Orientation));
        var center = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);
        Vector3 nearest = Vector3.Clamp(localOrigin, center - HALF_CELL, center + HALF_CELL);
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
    /// Resolves the target voxel coordinate from a physics raycast hit point and surface normal.
    /// Uses a small inward nudge for selection/breaking so thin and sloped hitboxes stay in the hit cell,
    /// and steps into the adjacent empty cell along the surface normal when placing.
    /// </summary>
    public static Int3 ResolveTargetCell(
        Vector3 hitPoint,
        Vector3 hitNormal,
        Vector3 structureOrigin,
        Quaternion structureOrientation,
        VoxelObject voxelObject,
        bool placeAdjacent
    )
    {
        const float SURFACE_EPSILON = 0.001f;

        Vector3 inwardPoint = hitPoint - hitNormal * SURFACE_EPSILON;
        Int3 hitCell = WorldToBrickSpace(inwardPoint, structureOrigin, structureOrientation);
        if (!placeAdjacent)
        {
            return hitCell;
        }

        //  Outer cell faces step straight into the adjacent empty cell.
        Vector3 outwardPoint = hitPoint + hitNormal * SURFACE_EPSILON;
        Int3 outwardCell = WorldToBrickSpace(outwardPoint, structureOrigin, structureOrientation);
        if (outwardCell != hitCell && voxelObject.Get(outwardCell.X, outwardCell.Y, outwardCell.Z).ID == 0)
        {
            return outwardCell;
        }

        //  Recessed or sloped faces (slabs, plates, columns, stairs, slopes, custom shapes) stay inside
        //  the hit cell after a tiny nudge. Step along the dominant local normal axis from the hit cell.
        Quaternion inverseOrientation = Quaternion.Inverse(structureOrientation);
        Vector3 localNormal = Vector3.Transform(hitNormal, inverseOrientation);
        Vector3 localHit = Vector3.Transform(hitPoint - structureOrigin, inverseOrientation);
        Vector3 cellOffset = localHit - new Vector3(hitCell.X, hitCell.Y, hitCell.Z);

        Int3 primaryStep = PickCardinalStep(localNormal, cellOffset, excludeAxis: -1, out int primaryAxis);
        var candidate = new Int3(hitCell.X + primaryStep.X, hitCell.Y + primaryStep.Y, hitCell.Z + primaryStep.Z);
        if (voxelObject.Get(candidate.X, candidate.Y, candidate.Z).ID == 0)
        {
            return candidate;
        }

        Int3 secondaryStep = PickCardinalStep(localNormal, cellOffset, excludeAxis: primaryAxis, out _);
        if (secondaryStep.X != 0 || secondaryStep.Y != 0 || secondaryStep.Z != 0)
        {
            var secondary = new Int3(hitCell.X + secondaryStep.X, hitCell.Y + secondaryStep.Y, hitCell.Z + secondaryStep.Z);
            if (voxelObject.Get(secondary.X, secondary.Y, secondary.Z).ID == 0)
            {
                return secondary;
            }
        }

        Vector3 marched = hitPoint;
        for (var step = 0; step < 10; step++)
        {
            marched += hitNormal * 0.25f;
            Int3 cell = WorldToBrickSpace(marched, structureOrigin, structureOrientation);
            if (cell != hitCell && voxelObject.Get(cell.X, cell.Y, cell.Z).ID == 0)
            {
                return cell;
            }
        }

        return candidate;
    }

    private static Int3 PickCardinalStep(Vector3 localNormal, Vector3 cellOffset, int excludeAxis, out int chosenAxis)
    {
        const float MIN_AXIS_COMPONENT = 0.01f;
        const float TIE_TOLERANCE = 0.05f;

        chosenAxis = -1;
        var bestMagnitude = 0f;
        var bestTieBreak = float.NegativeInfinity;
        var bestSign = 0;

        for (var axis = 0; axis < 3; axis++)
        {
            if (axis == excludeAxis)
            {
                continue;
            }

            float component = axis == 0 ? localNormal.X : (axis == 1 ? localNormal.Y : localNormal.Z);
            float magnitude = MathF.Abs(component);
            if (magnitude < MIN_AXIS_COMPONENT)
            {
                continue;
            }

            int sign = component >= 0f ? 1 : -1;
            float offsetAlongAxis = (axis == 0 ? cellOffset.X : (axis == 1 ? cellOffset.Y : cellOffset.Z)) * sign;

            bool isBetter = chosenAxis < 0
                || magnitude > bestMagnitude + TIE_TOLERANCE
                || (MathF.Abs(magnitude - bestMagnitude) <= TIE_TOLERANCE && offsetAlongAxis > bestTieBreak);

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