using System;

namespace Swordfish.Graphics;

/// <summary>Options controlling rasterization and shadow passes for a mesh.</summary>
public struct RenderOptions : IEquatable<RenderOptions>
{
    public static RenderOptions Default { get; } = new();

    public bool DoubleFaced;
    public bool Wireframe;
    public bool IgnoreDepth;
    public bool CastShadows = true;
    public bool ReceiveShadows = true;
    public AlphaMode AlphaMode = AlphaMode.Opaque;
    public float AlphaCutoff = 0.5f;

    public RenderOptions()
    {
    }

    /// <inheritdoc/>
    public bool Equals(RenderOptions other)
    {
        return DoubleFaced == other.DoubleFaced
            && Wireframe == other.Wireframe
            && IgnoreDepth == other.IgnoreDepth
            && CastShadows == other.CastShadows
            && ReceiveShadows == other.ReceiveShadows
            && AlphaMode == other.AlphaMode
            && AlphaCutoff.Equals(other.AlphaCutoff);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is RenderOptions other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(DoubleFaced, Wireframe, IgnoreDepth, CastShadows, ReceiveShadows, AlphaMode, AlphaCutoff);
    }

    public static bool operator ==(RenderOptions left, RenderOptions right) => left.Equals(right);
    public static bool operator !=(RenderOptions left, RenderOptions right) => !left.Equals(right);
}
