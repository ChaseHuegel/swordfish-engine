namespace Swordfish.Graphics;

public struct RenderOptions
{
    public static RenderOptions Default { get; } = new();

    public bool DoubleFaced;
    public bool Wireframe;
    public bool IgnoreDepth;
}