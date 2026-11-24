using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Reef;

/// <summary>
///     A width by height grid of <see cref="PixelRgba8" /> pixels.
/// </summary>
public class PixelTexture
{
    public readonly int Width;
    public readonly int Height;
    
    private readonly PixelRgba8[] _pixels;

    public PixelTexture(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new PixelRgba8[width * height];
    }

    /// <summary>
    ///     Returns a copy of every pixel as a color in the zero to one range.
    /// </summary>
    public ReadOnlyCollection<Vector4> GetPixels()
    {
        var pixels = new Vector4[_pixels.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = _pixels[i].ToVector4();
        }

        return Array.AsReadOnly(pixels);
    }

    public Vector4 GetPixel(int x, int y)
    {
        return _pixels[y * Width + x].ToVector4();
    }

    public void SetPixel(int x, int y, Vector4 color)
    {
        _pixels[y * Width + x] = PixelRgba8.FromVector4(color);
    }

    /// <summary>
    ///     Returns the pixels of one scanline in place, so a caller can read or
    ///     write a whole row without recomputing the row offset per pixel.
    /// </summary>
    public Span<PixelRgba8> GetRowSpan(int y)
    {
        return _pixels.AsSpan(y * Width, Width);
    }

    /// <summary>
    ///     Returns the whole pixel buffer without copying it.
    /// </summary>
    internal ReadOnlySpan<PixelRgba8> AsSpan()
    {
        return _pixels;
    }

    /// <summary>
    ///     Resets every pixel to transparent black.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_pixels);
    }
}
