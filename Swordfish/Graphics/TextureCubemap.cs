namespace Swordfish.Graphics;

public class TextureCubemap : Texture
{
    public Texture[] Textures { get; }

    public TextureCubemap(string name, Texture[] textures, bool mipmaps) : base(name, null!, 0, 0, mipmaps)
    {
        if (textures.Length != 6)
        {
            throw new ArgumentException("Cubemaps require 6 textures.", nameof(textures));
        }
        
        for (var i = 0; i < textures.Length; i++)
        {
            Texture texture = textures[i];

            if (texture.Width > Width)
            {
                Width = texture.Width;
            }

            if (texture.Height > Height)
            {
                Height = texture.Height;
            }
        }

        Textures = textures;
    }
}
