using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public class Material : Handle
{
    public Shader Shader { get; set; }

    public Texture[] Textures { get; set; }

    /// <summary>Alpha handling mode applied when rendering this material.</summary>
    public AlphaMode AlphaMode { get; set; } = AlphaMode.Opaque;

    /// <summary>Alpha discard threshold used when <see cref="AlphaMode"/> is <see cref="Graphics.AlphaMode.Masked"/>.</summary>
    public float AlphaCutoff { get; set; } = 0.5f;

    public Material(Shader shader, params Texture[] textures)
    {
        Shader = shader;
        Textures = textures;
    }

    protected override void OnDisposed()
    {
    }
}