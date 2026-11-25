namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Holds decoded, vertically flipped RGBA8 pixel data for a standalone 2D OpenGL preview texture.</summary>
public readonly struct StandaloneTextureData(int width, int height, byte[] rgbaPixels)
{
    /// <summary>Texture width in pixels.</summary>
    public readonly int Width = width;

    /// <summary>Texture height in pixels.</summary>
    public readonly int Height = height;

    /// <summary>Contiguous RGBA8 pixels vertically flipped for OpenGL bottom-up UV coordinates.</summary>
    public readonly byte[] RgbaPixels = rgbaPixels;

    /// <summary>True when dimensions and pixel buffer are valid for OpenGL upload.</summary>
    public bool IsValid => Width > 0 && Height > 0 && RgbaPixels != null && RgbaPixels.Length >= Width * Height * 4;
}
