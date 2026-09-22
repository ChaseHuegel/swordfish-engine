using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// The shared, single-location interaction resolver. Client prediction and server authority both call it
/// with their own world ray (client: screen-center camera ray; server: mirror transform + look) and their
/// own <see cref="IVoxelInteractionWorld"/>; both build colliders from the same shared voxel data, so the
/// ported targeting here picks the same cell from the same ray on both sides. The client's resolved cell
/// arrives as an optional <see cref="BrickInteraction"/> hint; presence of a hint governs whether a brick
/// interaction is attempted at all, and it is validated against the independently-derived cell.
/// </summary>
public static class SharedInteractionResolver
{
    public const float DEFAULT_REACH = 9.5f;

    private const float RAY_ORIGIN_OFFSET = 0.26f;
    private const float SURFACE_BIAS = 0.1f;
    private const float MARCH_BACK_STEP = 0.25f;
    private const int MARCH_BACK_STEPS = 10;
    private const float REACH_AROUND_WIDTH = 0.5f;
    private const float REACH_AROUND_RAY_LENGTH = 0.9f;

    /// <summary>
    /// Resolves a pressed interaction edge + optional target hint into an action against the world. A
    /// hint-less event (or an event whose hint does not resolve to a legal target) yields
    /// <see cref="InteractionAction.None"/> - it never throws or assumes a hint is present.
    /// </summary>
    public static InteractionResolution Resolve(
        in Ray ray,
        BrickInteraction? hint,
        InteractionKind kind,
        PlaceableBrick? placeable,
        GameMode gameMode,
        float reach,
        in IVoxelInteractionWorld world
    ) {
        if (hint == null)
        {
            return InteractionResolution.None;
        }

        bool isBreak = kind == InteractionKind.PrimaryPressed;
        bool isPlace = kind == InteractionKind.SecondaryPressed;
        if (!isBreak && !isPlace)
        {
            return InteractionResolution.None;
        }

        //  Resolve the targeted structure (with reach-around) from the world ray.
        if (!TryResolveTarget(ray, offset: isPlace, reachAround: true, reach, world, out InteractionTarget target) ||
            target.VoxelObject == null)
        {
            return InteractionResolution.None;
        }

        //  Plausibility: validate the client-sent hint cell against the structure's authoritative voxel
        //  container rather than demanding the authority's own ray derive the identical cell. The client
        //  aims with a screen-center camera ray while the authority aims with the body transform ray, so
        //  the two may legitimately land a cell apart at voxel boundaries; the hint cell is the intended
        //  target and is independently validated here for reach and occupancy.
        Int3 hintCell = new(hint.Value.TargetX, hint.Value.TargetY, hint.Value.TargetZ);

        //  Reach: the cell must be within reach of the interaction origin.
        Vector3 cellWorld = BrickToWorldSpace(hintCell, target.Transform.Position, target.Transform.Orientation);
        if (Vector3.Distance(ray.Origin, cellWorld) > reach)
        {
            return InteractionResolution.None;
        }

        Voxel hintVoxel = target.VoxelObject.Get(hintCell.X, hintCell.Y, hintCell.Z);

        if (isBreak)
        {
            //  Break requires an occupied cell.
            if (hintVoxel.ID == 0)
            {
                return InteractionResolution.None;
            }

            return new InteractionResolution(InteractionAction.Break, target.Entity, hintCell, hintVoxel);
        }

        //  Place requires an empty destination cell and a held placeable brick.
        if (hintVoxel.ID != 0 || placeable == null)
        {
            return InteractionResolution.None;
        }

        Voxel voxel = placeable.Value.ToVoxel((BrickShape)hint.Value.HintShape, new Orientation(hint.Value.HintOrientation));
        return new InteractionResolution(InteractionAction.Place, target.Entity, hintCell, voxel);
    }

    /// <summary>
    /// Resolves the target cell a given ray points at, using the same targeting the resolver internally
    /// applies. The client calls this to build its <see cref="BrickInteraction"/> hint from the shared
    /// code; the resolver's validation then validates that hint cell against the authoritative voxel
    /// container (reach + occupancy) rather than requiring the authority's own ray to re-derive the
    /// identical cell. Returns the brick-space cell without validating reach/occupancy — that is the
    /// resolver's job.
    /// </summary>
    public static bool TryResolveTargetCell(
        in Ray ray,
        bool offset,
        bool reachAround,
        float reach,
        in IVoxelInteractionWorld world,
        out Int3 coordinate
    ) {
        if (!TryResolveTarget(ray, offset, reachAround, reach, world, out InteractionTarget target))
        {
            coordinate = default;
            return false;
        }

        coordinate = target.Coordinate;
        return true;
    }

    /// <summary>
    /// Ports the client's screen-space brick targeting onto a raw world ray: raycast against the world,
    /// map the hit point into the structure's brick space, bias toward the surface (or, for placement,
    /// the adjacent empty cell), reach-around when the center ray misses, and march back along the normal
    /// to an empty destination. Both prediction and authority run this with their own ray + world.
    /// </summary>
    private static bool TryResolveTarget(
        in Ray ray,
        bool offset,
        bool reachAround,
        float reach,
        in IVoxelInteractionWorld world,
        out InteractionTarget target
    ) {
        target = default;

        Vector3 direction = Vector3.Normalize(ray.Vector);
        Ray castRay = new(ray.Origin + direction * RAY_ORIGIN_OFFSET, direction * reach);

        Vector3? reachAroundDir = null;
        if (!TryRaycastStructure(castRay, world, out RaycastResult raycast, out VoxelObject? voxelObject, out TransformComponent transform) ||
            voxelObject == null)
        {
            if (!reachAround ||
                !TryReachAroundRaycasts(castRay, world, ref reachAroundDir, out raycast, out voxelObject, out transform) ||
                voxelObject == null)
            {
                return false;
            }
        }

        Vector3 worldPos = raycast.Point;
        if (offset && reachAroundDir == null)
        {
            //  Placement biases toward the destination cell adjacent to the hit surface.
            worldPos += raycast.Normal * SURFACE_BIAS;
        }
        else
        {
            worldPos += raycast.Normal * -SURFACE_BIAS;
        }

        Int3 coordinate = WorldToBrickSpace(worldPos, transform.Position, transform.Orientation);
        Voxel voxel = voxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);

        if (reachAroundDir != null)
        {
            TryGetRelativeBrickInWorldSpace(voxelObject, transform, reachAroundDir.Value, ref coordinate, out voxel);
        }

        //  Placement looks for an empty destination: march back along the hit normal until one is found.
        for (var step = 0; offset && voxel.ID != 0 && step < MARCH_BACK_STEPS; step++)
        {
            worldPos -= raycast.Normal * MARCH_BACK_STEP;
            coordinate = WorldToBrickSpace(worldPos, transform.Position, transform.Orientation);
            voxel = voxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
        }

        target = new InteractionTarget(raycast.Entity.Ptr, transform, voxelObject, coordinate);
        return true;
    }

    private static bool TryReachAroundRaycasts(
        in Ray centerRay,
        in IVoxelInteractionWorld world,
        ref Vector3? reachAroundDir,
        out RaycastResult raycast,
        out VoxelObject? voxelObject,
        out TransformComponent transform
    ) {
        Vector3 basisForward = Vector3.Normalize(centerRay.Vector);
        Vector3 basisRight = Vector3.Normalize(Vector3.Cross(basisForward, Math.Abs(Vector3.Dot(basisForward, Vector3.UnitY)) < 0.9f ? Vector3.UnitY : Vector3.UnitX));
        Vector3 basisUp = Vector3.Cross(basisRight, basisForward);

        if (TryReachAroundRaycast(centerRay, basisUp, ref reachAroundDir, world, out raycast, out voxelObject, out transform))
        {
            return true;
        }

        return TryReachAroundRaycast(centerRay, basisRight, ref reachAroundDir, world, out raycast, out voxelObject, out transform);
    }

    private static bool TryReachAroundRaycast(
        in Ray centerRay,
        in Vector3 direction,
        ref Vector3? reachAroundDir,
        in IVoxelInteractionWorld world,
        out RaycastResult raycast,
        out VoxelObject? voxelObject,
        out TransformComponent transform
    ) {
        Ray ray = new(centerRay.Origin + direction * REACH_AROUND_WIDTH, centerRay.Vector * REACH_AROUND_RAY_LENGTH);
        if (TryRaycastStructure(ray, world, out raycast, out voxelObject, out transform) && voxelObject != null)
        {
            reachAroundDir = -direction;
            return true;
        }

        ray = new Ray(centerRay.Origin + direction * -REACH_AROUND_WIDTH, centerRay.Vector * REACH_AROUND_RAY_LENGTH);
        if (TryRaycastStructure(ray, world, out raycast, out voxelObject, out transform) && voxelObject != null)
        {
            reachAroundDir = direction;
            return true;
        }

        voxelObject = null;
        transform = default;
        return false;
    }

    private static bool TryRaycastStructure(
        in Ray ray,
        in IVoxelInteractionWorld world,
        out RaycastResult raycast,
        out VoxelObject? voxelObject,
        out TransformComponent transform
    ) {
        raycast = world.TryRaycast(ray, out RaycastResult result) ? result : default;
        if (!raycast.Hit)
        {
            voxelObject = null;
            transform = default;
            return false;
        }

        return world.TryGetVoxelTarget(raycast.Entity.Ptr, out voxelObject, out transform);
    }

    private static void TryGetRelativeBrickInWorldSpace(
        VoxelObject voxelObject,
        TransformComponent transform,
        Vector3 worldNormal,
        ref Int3 coordinate,
        out Voxel voxel
    ) {
        Vector3 worldPos = BrickToWorldSpace(coordinate, transform.Position, transform.Orientation);

        worldNormal = new Vector3(
            (float)Math.Round(worldNormal.X, MidpointRounding.AwayFromZero),
            (float)Math.Round(worldNormal.Y, MidpointRounding.AwayFromZero),
            (float)Math.Round(worldNormal.Z, MidpointRounding.AwayFromZero)
        );

        worldPos += worldNormal;
        coordinate = WorldToBrickSpace(worldPos, transform.Position, transform.Orientation);
        voxel = voxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z);
    }

    public static Int3 WorldToBrickSpace(Vector3 position, Vector3 origin, Quaternion orientation)
    {
        Vector3 localPos = Vector3.Transform(position - origin, Quaternion.Inverse(orientation)) + new Vector3(0.5f);

        var x = (int)Math.Floor(localPos.X);
        var y = (int)Math.Floor(localPos.Y);
        var z = (int)Math.Floor(localPos.Z);

        return new Int3(x, y, z);
    }

    public static Vector3 BrickToWorldSpace(Int3 coordinate, Vector3 origin, Quaternion orientation)
    {
        var localCenter = new Vector3(coordinate.X, coordinate.Y, coordinate.Z);
        return Vector3.Transform(localCenter, orientation) + origin;
    }

    private readonly struct InteractionTarget
    {
        public readonly int Entity;
        public readonly TransformComponent Transform;
        public readonly VoxelObject? VoxelObject;
        public readonly Int3 Coordinate;

        public InteractionTarget(int entity, TransformComponent transform, VoxelObject? voxelObject, Int3 coordinate)
        {
            Entity = entity;
            Transform = transform;
            VoxelObject = voxelObject;
            Coordinate = coordinate;
        }
    }
}
