using Swordfish.ECS;

namespace Swordfish.Graphics;

public struct RectRendererComponent : IDataComponent
{
    public readonly RectRenderer RectRenderer;

    public bool Bound;

    public RectRendererComponent(RectRenderer rectRenderer)
    {
        RectRenderer = rectRenderer;
    }
}
