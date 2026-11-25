using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Shared region placement geometry and world-overlap checks for client preview and server authority.</summary>
public static class RegionPlacementResolver
{
    private const float PLACEMENT_PENETRATION_EPSILON = 0.005f;

    /// <summary>Computes the structure-local paste origin from a hit cell and surface normal.</summary>
    public static Int3 ComputeFlushOrigin(
        Int3 hitCell,
        Vector3 localHitNormal,
        Int3 regionSize,
        bool embedAtHitCell
    )
    {
        if (embedAtHitCell)
        {
            return new Int3(
                hitCell.X - regionSize.X / 2,
                hitCell.Y - regionSize.Y / 2,
                hitCell.Z - regionSize.Z / 2
            );
        }

        Int3 axis = SnapToCardinalAxis(localHitNormal);
        int originX = axis.X > 0
            ? hitCell.X + 1
            : (axis.X < 0 ? hitCell.X - regionSize.X : hitCell.X - regionSize.X / 2);
        int originY = axis.Y > 0
            ? hitCell.Y + 1
            : (axis.Y < 0 ? hitCell.Y - regionSize.Y : hitCell.Y - regionSize.Y / 2);
        int originZ = axis.Z > 0
            ? hitCell.Z + 1
            : (axis.Z < 0 ? hitCell.Z - regionSize.Z : hitCell.Z - regionSize.Z / 2);

        return new Int3(originX, originY, originZ);
    }

    /// <summary>Computes the world-space transform for spawning a centered standalone structure in front of the camera.</summary>
    public static void ComputeFreeSpacePose(
        Vector3 cameraPosition,
        Quaternion cameraOrientation,
        float distance,
        Int3 regionSize,
        out Vector3 spawnPosition,
        out Quaternion spawnOrientation
    )
    {
        float clampedDistance = Math.Clamp(distance, CharacterDefaults.REGION_FREE_DISTANCE_MIN, CharacterDefaults.REGION_FREE_DISTANCE_MAX);
        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, cameraOrientation);
        Vector3 targetCenter = cameraPosition + forward * clampedDistance;

        var centerOffset = new Int3(-(regionSize.X / 2), -(regionSize.Y / 2), -(regionSize.Z / 2));
        var localBoundsCenter = new Vector3(
            centerOffset.X + (regionSize.X - 1) * 0.5f,
            centerOffset.Y + (regionSize.Y - 1) * 0.5f,
            centerOffset.Z + (regionSize.Z - 1) * 0.5f
        );

        spawnOrientation = Quaternion.Identity;
        spawnPosition = targetCenter - Vector3.Transform(localBoundsCenter, spawnOrientation);
    }

    /// <summary>Returns whether placing <paramref name="region"/> at <paramref name="origin"/> intersects any collider other than <paramref name="ignoredEntity"/>.</summary>
    public static bool IntersectsWorldColliders(
        IPhysics physics,
        int ignoredEntity,
        in TransformComponent targetTransform,
        Int3 origin,
        in VoxelRegionData region,
        IBrickDatabase? brickDatabase
    )
    {
        if (region.SizeX <= 0 || region.SizeY <= 0 || region.SizeZ <= 0 || region.Chunks == null)
        {
            return false;
        }

        var tempObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        int solidCount = VoxelRegionApplier.ApplyTo(tempObject, in region, origin, overwriteAir: false);
        if (solidCount == 0)
        {
            return false;
        }

        CompoundShape compound = VoxelCollision.BuildCompound(tempObject, brickDatabase);
        if (compound.Shapes.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < compound.Shapes.Length; i++)
        {
            Vector3 worldPosition = Vector3.Transform(compound.Positions[i] * targetTransform.Scale, targetTransform.Orientation)
                + targetTransform.Position;
            Quaternion worldOrientation = targetTransform.Orientation * compound.Orientations[i];

            var query = new ShapeOverlapQuery(
                compound.Shapes[i],
                worldPosition,
                worldOrientation,
                targetTransform.Scale,
                ignoredEntity: ignoredEntity,
                minPenetration: PLACEMENT_PENETRATION_EPSILON
            );

            if (physics.Overlaps(in query))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether any point inside the structure-local box is within <paramref name="reach"/> of <paramref name="worldOrigin"/>.</summary>
    public static bool IsBoxWithinReach(
        Int3 min,
        Int3 max,
        Vector3 worldOrigin,
        in TransformComponent transform,
        float reach
    )
    {
        Vector3 localOrigin = Vector3.Transform(worldOrigin - transform.Position, Quaternion.Inverse(transform.Orientation));
        Vector3 scaledA = new Vector3(min.X - 0.5f, min.Y - 0.5f, min.Z - 0.5f) * transform.Scale;
        Vector3 scaledB = new Vector3(max.X + 0.5f, max.Y + 0.5f, max.Z + 0.5f) * transform.Scale;
        Vector3 boxMin = Vector3.Min(scaledA, scaledB);
        Vector3 boxMax = Vector3.Max(scaledA, scaledB);
        Vector3 nearest = Vector3.Clamp(localOrigin, boxMin, boxMax);
        return Vector3.DistanceSquared(localOrigin, nearest) <= reach * reach;
    }

    /// <summary>Returns whether a world position is within <paramref name="reach"/> of <paramref name="worldOrigin"/>.</summary>
    public static bool IsPointWithinReach(Vector3 point, Vector3 worldOrigin, float reach)
    {
        return Vector3.DistanceSquared(point, worldOrigin) <= reach * reach;
    }

    private static Int3 SnapToCardinalAxis(Vector3 normal)
    {
        float ax = MathF.Abs(normal.X);
        float ay = MathF.Abs(normal.Y);
        float az = MathF.Abs(normal.Z);

        if (ax >= ay && ax >= az)
        {
            return new Int3(normal.X >= 0f ? 1 : -1, 0, 0);
        }

        if (ay >= az)
        {
            return new Int3(0, normal.Y >= 0f ? 1 : -1, 0);
        }

        return new Int3(0, 0, normal.Z >= 0f ? 1 : -1);
    }
}
