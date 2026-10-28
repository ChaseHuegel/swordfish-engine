namespace Swordfish.Graphics;

/// <summary>Options controlling rasterization and shadow passes for a mesh.</summary>
public struct RenderOptions
{
    public static RenderOptions Default { get; } = new();

    public bool DoubleFaced;
    public bool Wireframe;
    public bool IgnoreDepth;
    public bool CastShadows = true;
    public bool ReceiveShadows = true;
    public AlphaMode AlphaMode = AlphaMode.Opaque;
    public float AlphaCutoff = 0.5f;

    public RenderOptions()
    {
    }
}
