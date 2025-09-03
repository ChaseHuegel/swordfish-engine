using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;

namespace Reef;

public class PixelTexture
{
    public readonly int Width;
    public readonly int Height;
    
    private readonly Vector4[] _pixels;

    public PixelTexture(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new Vector4[width * height];
    }

    public ReadOnlyCollection<Vector4> GetPixels()
    {
        return _pixels.AsReadOnly();
    }

    public Vector4 GetPixel(int x, int y)
    {
        return _pixels[y * Width + x];
    }

    public void SetPixel(int x, int y, Vector4 color)
    {
        _pixels[y * Width + x] = color;
    }
}