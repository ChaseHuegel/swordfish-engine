using System;
using System.Numerics;
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
    public static bool IsReachable(in Int3 coordinate, Vector3 origin, in TransformComponent transform, float reach)
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

        //  A voxel at integer coordinate c spans [c, c+1); floor maps any point in that span to c.
        var x = (int)Math.Floor(localPos.X);
        var y = (int)Math.Floor(localPos.Y);
        var z = (int)Math.Floor(localPos.Z);

        return new Int3(x, y, z);
    }

    /// <summary>Maps a brick-space coordinate to its world position (the voxel's origin corner, not its center).</summary>
    public static Vector3 BrickToWorldSpace(Int3 coordinate, Vector3 origin, Quaternion orientation)
    {
        var localPosition = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);
        return Vector3.Transform(localPosition, orientation) + origin;
    }
}