using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Physics;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Shared physics aim-ray tracing along a character transform's forward direction.</summary>
public static class AimRay
{
    /// <summary>Returns the normalized world-space look direction for <paramref name="transform"/>.</summary>
    public static Vector3 GetLookDirection(in TransformComponent transform)
    {
        Vector3 raw = Vector3.Transform(-Vector3.UnitZ, transform.Orientation);
        float lenSq = raw.LengthSquared();
        if (!float.IsFinite(lenSq) || lenSq <= 1e-6f)
        {
            return -Vector3.UnitZ;
        }

        return raw / MathF.Sqrt(lenSq);
    }

    /// <summary>
    /// Traces from <paramref name="transform"/> along its look direction up to <paramref name="reach"/>,
    /// ignoring <paramref name="ignoreEntity"/> when casting against physics bodies.
    /// </summary>
    public static bool TryTrace(
        IPhysics physics,
        int ignoreEntity,
        in TransformComponent transform,
        float reach,
        out Vector3 endpoint,
        out Vector3 normal,
        out Entity hitEntity
    ) {
        if (!IsFinite(in transform.Position) || !float.IsFinite(reach) || reach <= 0f)
        {
            endpoint = Vector3.Zero;
            normal = Vector3.Zero;
            hitEntity = default;
            return false;
        }

        Vector3 direction = GetLookDirection(in transform);
        RaycastResult hit = physics.Raycast(new Ray(transform.Position, direction * reach), ignoreEntity);

        if (hit.Hit && hit.Entity.Ptr != ignoreEntity && IsFinite(in hit.Point))
        {
            endpoint = hit.Point;
            normal = SafeNormalize(in hit.Normal);
            hitEntity = hit.Entity;
            return true;
        }

        endpoint = transform.Position + direction * reach;
        normal = Vector3.Zero;
        hitEntity = default;
        return false;
    }

    private static Vector3 SafeNormalize(in Vector3 vector)
    {
        float lenSq = vector.LengthSquared();
        if (!float.IsFinite(lenSq) || lenSq <= 1e-6f)
        {
            return Vector3.UnitY;
        }

        return vector / MathF.Sqrt(lenSq);
    }

    private static bool IsFinite(in Vector3 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
    }
}
