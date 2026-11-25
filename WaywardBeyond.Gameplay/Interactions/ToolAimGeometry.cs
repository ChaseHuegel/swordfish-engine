using System;
using System.Numerics;
using Swordfish.ECS;

namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Shared tool aim, nozzle origin, and finiteness helpers across client and server.</summary>
public static class ToolAimGeometry
{
    /// <summary>Offset along the surface normal for impact mist, sparks, and coat bursts.</summary>
    public const float IMPACT_EMITTER_OFFSET = 0.04f;

    private static readonly Vector3 _closeFallbackLocal = new(0.18f, -0.15f, -0.15f);
    private static readonly Vector3 _pointBlankFallbackLocal = new(0.12f, -0.10f, 0f);
    private static readonly Vector3 _remoteOriginLocal = new(0.25f, -0.25f, -0.35f);

    /// <summary>Computes an approximate third-person emitter origin for a remote player.</summary>
    public static Vector3 ComputeRemoteOrigin(in TransformComponent remoteTransform)
    {
        return remoteTransform.Position + Vector3.Transform(_remoteOriginLocal * remoteTransform.Scale, remoteTransform.Orientation);
    }

    /// <summary>
    /// Ensures the first-person tool origin stays behind <paramref name="targetWorld"/> along the look
    /// vector so close-range surfaces never invert beams or mist.
    /// </summary>
    public static Vector3 ComputeSafeStart(in TransformComponent playerTransform, Vector3 muzzleWorld, Vector3 targetWorld)
    {
        Vector3 lookDir = AimRay.GetLookDirection(in playerTransform);
        if (Vector3.Dot(targetWorld - muzzleWorld, lookDir) > 0.05f)
        {
            return muzzleWorld;
        }

        Vector3 closeOrigin = playerTransform.Position + Vector3.Transform(_closeFallbackLocal * playerTransform.Scale, playerTransform.Orientation);
        if (Vector3.Dot(targetWorld - closeOrigin, lookDir) > 0.05f)
        {
            return closeOrigin;
        }

        return playerTransform.Position + Vector3.Transform(_pointBlankFallbackLocal * playerTransform.Scale, playerTransform.Orientation);
    }

    /// <summary>Computes a normalized world-space aim direction from <paramref name="startWorld"/> toward <paramref name="targetWorld"/>.</summary>
    public static Vector3 ComputeAimDirection(in TransformComponent playerTransform, Vector3 startWorld, Vector3 targetWorld)
    {
        Vector3 delta = targetWorld - startWorld;
        float lenSq = delta.LengthSquared();
        if (float.IsFinite(lenSq) && lenSq > 1e-4f)
        {
            return delta / MathF.Sqrt(lenSq);
        }

        return AimRay.GetLookDirection(in playerTransform);
    }

    /// <summary>Returns true when all components of <paramref name="vector"/> are finite.</summary>
    public static bool IsFinite(in Vector3 vector)
    {
        return float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
    }

    /// <summary>Returns true when all components of <paramref name="quaternion"/> are finite.</summary>
    public static bool IsFinite(in Quaternion quaternion)
    {
        return float.IsFinite(quaternion.X)
            && float.IsFinite(quaternion.Y)
            && float.IsFinite(quaternion.Z)
            && float.IsFinite(quaternion.W);
    }
}
