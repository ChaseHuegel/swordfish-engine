using Swordfish.ECS;

namespace Swordfish.Graphics;

public struct MeshRendererComponent : IDataComponent
{
    public const int DefaultIndex = 3;

    public readonly MeshRenderer MeshRenderer;

    public bool Bound;

    public MeshRendererComponent(MeshRenderer meshRenderer)
    {
        MeshRenderer = meshRenderer;
    }
}
