namespace Swordfish.Graphics;

/// <summary>
///     Defines the color space of a texture asset.
/// </summary>
public enum ColorSpace
{
    /// <summary>
    ///     Standard RGB transfer function (IEC 61966-2-1). Decoded to linear on sample.
    /// </summary>
    Srgb,

    /// <summary>
    ///     Linear data (normals, metallic, smoothness, distance fields). Sampled without conversion.
    /// </summary>
    Linear,
}
