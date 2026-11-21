using System;

namespace WaywardBeyond.Voxels;

/// <summary>Sparse per-face paint override carrying optional texture paint and color tint paint IDs.</summary>
public readonly struct FaceOverride(ushort texturePaintId, ushort colorPaintId = 0) : IEquatable<FaceOverride>
{
    /// <summary>An empty override (no texture paint and no color tint).</summary>
    public static readonly FaceOverride Empty = default;

    /// <summary>Paint registry ID for a texture override, or 0 to inherit the brick texture.</summary>
    public readonly ushort TexturePaintId = texturePaintId;

    /// <summary>Paint registry ID for a color tint override, or 0 for untinted.</summary>
    public readonly ushort ColorPaintId = colorPaintId;

    /// <summary>True when neither texture paint nor color tint is set.</summary>
    public bool IsEmpty => TexturePaintId == 0 && ColorPaintId == 0;

    /// <summary>Returns a copy with <see cref="TexturePaintId"/> replaced.</summary>
    public FaceOverride WithTexture(ushort texturePaintId) => new(texturePaintId, ColorPaintId);

    /// <summary>Returns a copy with <see cref="ColorPaintId"/> replaced.</summary>
    public FaceOverride WithColor(ushort colorPaintId) => new(TexturePaintId, colorPaintId);

    /// <inheritdoc/>
    public bool Equals(FaceOverride other) => TexturePaintId == other.TexturePaintId && ColorPaintId == other.ColorPaintId;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FaceOverride other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(TexturePaintId, ColorPaintId);

    public static bool operator ==(FaceOverride left, FaceOverride right) => left.Equals(right);
    public static bool operator !=(FaceOverride left, FaceOverride right) => !left.Equals(right);
}
