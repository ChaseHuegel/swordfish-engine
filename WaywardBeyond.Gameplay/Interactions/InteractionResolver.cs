using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Voxels;
using WaywardBeyond.Voxels.Models;
using WaywardBeyond.Voxels.Numerics;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>
/// The shared, single-location interaction resolver. Client prediction and server authority both call it
/// to validate a client-sent <see cref="BrickInteraction"/> hint (structure identity + target cell) purely
/// against the world: the hint structure must resolve within reach and satisfy occupancy/held-item rules.
/// Server validation never raycasts - the client's screen-aim targeting is the only place a world ray is
/// used, and its result (structure + cell) is what arrives as the hint. An absent or invalid hint resolves
/// to <see cref="InteractionAction.None"/>.
/// </summary>
public static class InteractionResolver
{
    public const float DEFAULT_REACH = 9.5f;

    /// <summary>
    /// Validates a pressed interaction edge + target hint into an action against the world without
    /// raycasting: resolves the hinted structure by identity, checks reach (squared) from the interaction
    /// origin to the touched cell, and applies occupancy/held-item rules. A hint-less event (or one whose
    /// hint does not resolve to a legal target) yields <see cref="InteractionAction.None"/> - it never
    /// throws or assumes a hint is present.
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
        if (!world.TryGetVoxelTarget(Uuid.FromValue(hint.TargetEntity), out int entity, out VoxelObject? voxelObject, out TransformComponent transform) ||
            voxelObject == null)
        {
            return InteractionResolution.None;
        }

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