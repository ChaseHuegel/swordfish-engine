using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public class Material : Handle
{
    public Shader Shader { get; set; }

    public Texture[] Textures { get; set; }

    public bool Transparent { get; set; }

    public Material(Shader shader, params Texture[] textures)
    {
        Shader = shader;
        Textures = textures;
    }

    protected override void OnDisposed()
    {
    }
}