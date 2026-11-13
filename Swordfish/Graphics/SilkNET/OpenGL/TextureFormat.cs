using Silk.NET.OpenGL;

namespace Swordfish.Graphics.SilkNET.OpenGL;

internal struct TextureFormat(in InternalFormat internalFormat, in PixelFormat pixelFormat, in PixelType pixelType)
{
    public InternalFormat InternalFormat = internalFormat;
    public PixelFormat PixelFormat = pixelFormat;
    public PixelType PixelType = pixelType;

    public static readonly TextureFormat Depth16f = new(InternalFormat.DepthComponent16, PixelFormat.DepthComponent, PixelType.Float);
    public static readonly TextureFormat Depth24f = new(InternalFormat.DepthComponent24, PixelFormat.DepthComponent, PixelType.Float);
    public static readonly TextureFormat R16f = new(InternalFormat.R16f, PixelFormat.Red, PixelType.Float);
    public static readonly TextureFormat R32f = new(InternalFormat.R32f, PixelFormat.Red, PixelType.Float);
    public static readonly TextureFormat Rgb = new(InternalFormat.Rgb, PixelFormat.Rgb, PixelType.UnsignedByte);
    public static readonly TextureFormat Rgba = new(InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte);
    public static readonly TextureFormat Srgb8Alpha8 = new(InternalFormat.Srgb8Alpha8, PixelFormat.Rgba, PixelType.UnsignedByte);
    public static readonly TextureFormat R11fG11fB10f = new(InternalFormat.R11fG11fB10f, PixelFormat.Rgb, PixelType.Float);
    public static readonly TextureFormat Rgb16f = new(InternalFormat.Rgb16f, PixelFormat.Rgb, PixelType.Float);
    public static readonly TextureFormat Rgba16f = new(InternalFormat.Rgba16f, PixelFormat.Rgba, PixelType.Float);
}