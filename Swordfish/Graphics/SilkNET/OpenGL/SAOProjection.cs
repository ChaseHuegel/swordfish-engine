using System.Numerics;

namespace Swordfish.Graphics.SilkNET.OpenGL;

/// <summary>Constants that reconstruct a view-space position from screen UV and depth.</summary>
internal readonly struct SAOProjection
{
    public readonly Vector2 ViewScale;
    public readonly float DepthA;
    public readonly float DepthB;

    private SAOProjection(Vector2 viewScale, float depthA, float depthB)
    {
        ViewScale = viewScale;
        DepthA = depthA;
        DepthB = depthB;
    }

    /// <summary>Creates the constants for a right-handed perspective projection.</summary>
    public static SAOProjection FromProjection(in Matrix4x4 projection)
    {
        return new SAOProjection(
            new Vector2(1f / projection.M11, 1f / projection.M22),
            projection.M43,
            projection.M33
        );
    }
}
