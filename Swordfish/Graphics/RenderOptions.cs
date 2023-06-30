namespace Swordfish.Graphics;

public struct RenderOptions
{
    public static RenderOptions Default { get; } = new RenderOptions();

    public bool DoubleFaced;
    public bool Wireframe;
}