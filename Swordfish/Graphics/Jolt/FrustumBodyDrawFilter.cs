using System.Numerics;
using JoltPhysicsSharp;

namespace Swordfish.Graphics.Jolt;

/// <summary>
///     Filters Jolt bodies against the active camera frustum before debug geometry is generated.
/// </summary>
internal sealed class FrustumBodyDrawFilter : BodyDrawFilter
{
    private Frustum? _frustum;

    /// <summary>Sets the frustum tested for subsequent <see cref="ShouldDraw"/> calls.</summary>
    public void SetFrustum(in Frustum? frustum)
    {
        _frustum = frustum;
    }

    /// <inheritdoc/>
    protected override bool ShouldDraw(Body body)
    {
        JoltPhysicsSharp.BoundingBox bounds = body.WorldSpaceBounds;
        return ShouldDrawBounds(_frustum, bounds.Min, bounds.Max);
    }

    /// <summary>Tests whether an axis-aligned box intersects the optional frustum.</summary>
    internal static bool ShouldDrawBounds(in Frustum? frustum, in Vector3 min, in Vector3 max)
    {
        if (!frustum.HasValue)
        {
            return true;
        }

        Vector3 center = (min + max) * 0.5f;
        Vector3 extents = (max - min) * 0.5f;
        if (!IsFinite(center) || !IsFinite(extents))
        {
            return true;
        }

        return frustum.Value.Intersects(center, extents);
    }

    private static bool IsFinite(in Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    }
}
