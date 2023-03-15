using Swordfish.ECS;

namespace Swordfish.Graphics;

[Component]
public class MeshRendererComponent
{
    public const int DefaultIndex = 3;

    public MeshRenderer? MeshRenderer;

    public bool Bound;

    public MeshRendererComponent() { }

    public MeshRendererComponent(MeshRenderer meshRenderer)
    {
        MeshRenderer = meshRenderer;
    }
}
