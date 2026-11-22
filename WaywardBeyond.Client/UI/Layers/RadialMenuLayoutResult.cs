using System.Numerics;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Computed positions and hit-target size for a radial selector.</summary>
internal readonly struct RadialMenuLayoutResult(Vector2[] positions, int hitSize, int ringCount)
{
    public readonly Vector2[] Positions = positions;
    public readonly int HitSize = hitSize;
    public readonly int RingCount = ringCount;
}