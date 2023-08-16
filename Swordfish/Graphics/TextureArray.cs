using Swordfish.Library.Annotations;
using Swordfish.Library.Collections;

namespace Swordfish.Graphics;

public class TextureArray : Texture
{
    public int Depth { get; protected set; }

    private readonly SwitchDictionary<string, Texture, int> TextureIndices = new();

    public TextureArray([NotNull] string name, [NotNull] Texture[] textures, bool mipmaps) : base(name, null!, 0, 0, mipmaps)
    {
        Depth = textures.Length;

        List<byte> pixels = new();
        for (int i = 0; i < textures.Length; i++)
        {
            Texture texture = textures[i];

            if (texture.Width > Width)
                Width = texture.Width;

            if (texture.Height > Height)
                Height = texture.Height;

            pixels.AddRange(texture.Pixels);
            TextureIndices.Add(texture.Name, texture, i);
        }

        Pixels = pixels.ToArray();
    }

    public int IndexOf(string name)
    {
        if (TextureIndices.TryGetValue(name, out int index))
            return index;

        return -1;
    }

    public int IndexOf(Texture texture)
    {
        if (TextureIndices.TryGetValue(texture, out int index))
            return index;

        return -1;
    }
}
