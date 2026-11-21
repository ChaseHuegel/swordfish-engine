using System.Numerics;
using Swordfish.Library.Types.Shapes;

namespace Swordfish.Physics;

/// <summary>Describes a world-space shape overlap query against physics bodies.</summary>
public readonly struct ShapeOverlapQuery(
    in Shape shape,
    in Vector3 position,
    in Quaternion orientation,
    in Vector3 scale,
    int ignoredEntity = -1,
    float minPenetration = 0f
) {
    /// <summary>The shape to test.</summary>
    public readonly Shape Shape = shape;

    /// <summary>The world-space position of the shape origin.</summary>
    public readonly Vector3 Position = position;

    /// <summary>The world-space orientation of the shape.</summary>
    public readonly Quaternion Orientation = orientation;

    /// <summary>The scale applied to the shape.</summary>
    public readonly Vector3 Scale = scale;

    /// <summary>An optional entity index to exclude from the query, or a negative value to include all bodies.</summary>
    public readonly int IgnoredEntity = ignoredEntity;

    /// <summary>Contacts with penetration depth at or below this threshold are ignored.</summary>
    public readonly float MinPenetration = minPenetration;
}
