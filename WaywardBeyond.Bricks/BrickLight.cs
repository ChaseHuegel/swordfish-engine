using System.Numerics;

namespace WaywardBeyond.Bricks;

/// <summary>Validated point-light configuration bound to a <see cref="BrickEntity"/>.</summary>
public readonly record struct BrickLight(
    float? Radius,
    float Size,
    Vector3 Color,
    float Intensity,
    bool? CastShadows,
    Vector3 Offset,
    bool TintFromPaint
);
