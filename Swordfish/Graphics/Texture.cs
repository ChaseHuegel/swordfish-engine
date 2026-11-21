using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public class Texture : Handle
{
    public string Name { get; protected set; }
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public bool Mipmaps { get; protected set; }
    public ColorSpace ColorSpace { get; protected set; }
    public TextureFilter Filter { get; protected set; }
    public byte[] Pixels { get; protected set; }

    public Texture(byte[] pixels, int width, int height, bool mipmaps, ColorSpace colorSpace = ColorSpace.Srgb, TextureFilter filter = TextureFilter.Nearest)
        : this("unknown", pixels, width, height, mipmaps, colorSpace, filter) { }

    public Texture(string name, byte[] pixels, int width, int height, bool mipmaps, ColorSpace colorSpace = ColorSpace.Srgb, TextureFilter filter = TextureFilter.Nearest)
    {
        Name = name;
        Pixels = pixels;
        Width = width;
        Height = height;
        Mipmaps = mipmaps;
        ColorSpace = colorSpace;
        Filter = filter;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
