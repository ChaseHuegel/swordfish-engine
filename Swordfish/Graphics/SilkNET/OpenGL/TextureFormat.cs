using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal struct TextureFormat(in InternalFormat internalFormat, in PixelFormat pixelFormat, in PixelType pixelType)
{
    public InternalFormat InternalFormat = internalFormat;
    public PixelFormat PixelFormat = pixelFormat;
    public PixelType PixelType = pixelType;

    public static readonly TextureFormat RgbaByte = new(InternalFormat.Rgba, PixelFormat.Rgba, PixelType.Byte);
    public static readonly TextureFormat Depth24f = new(InternalFormat.DepthComponent24, PixelFormat.DepthComponent, PixelType.Float);
    public static readonly TextureFormat R32f = new(InternalFormat.R32f, PixelFormat.Red, PixelType.Float);
    public static readonly TextureFormat Rgb16f = new(InternalFormat.Rgba16f, PixelFormat.Rgb, PixelType.Float);
}