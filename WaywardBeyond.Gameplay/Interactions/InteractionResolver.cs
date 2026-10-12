using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Voxels;
using WaywardBeyond.Voxels.Models;
using WaywardBeyond.Voxels.Numerics;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
///     Shared interaction validation for client prediction and server authority. A hint-less or invalid hint
///     resolves to <see cref="InteractionAction.None"/>.
/// </summary>
public static class InteractionResolver
{
    /// <summary>The default interaction reach from the origin.</summary>
    public const float DEFAULT_REACH = 9.5f;

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
        float reachSquared = request.Reach * request.Reach;

        //  Reach: the touched voxel position must be within the player's reach of the interaction
        //  origin. A squared distance check keeps validation cheap.
        Vector3 cellWorld = BrickToWorldSpace(hintCell, transform.Position, transform.Orientation);
        if (Vector3.DistanceSquared(request.Origin, cellWorld) > reachSquared)
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

        Voxel voxel = request.Placeable.ToVoxel((BrickShape)hint.HintShape, brickRegistry, new Orientation(hint.HintOrientation));
        return new InteractionResolution(InteractionAction.Place, entity, hintCell, voxel);
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