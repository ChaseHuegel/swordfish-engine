using Swordfish.Library.Annotations;

namespace Swordfish.Graphics;

public class Texture : Handle
{
    public string Name { get; protected set; }
    public int Width { get; protected set; }
    public int Height { get; protected set; }
    public bool Mipmaps { get; protected set; }
    public byte[] Pixels { get; protected set; }

    public Texture([NotNull] byte[] pixels, int width, int height, bool mipmaps)
        : this("unknown", pixels, width, height, mipmaps) { }

    public Texture([NotNull] string name, [NotNull] byte[] pixels, int width, int height, bool mipmaps)
    {
        Name = name;
        Pixels = pixels;
        Width = width;
        Height = height;
        Mipmaps = mipmaps;
    }

    protected override void OnDisposed()
    {
        //  Do nothing
    }
}
