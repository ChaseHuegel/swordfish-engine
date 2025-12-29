using Swordfish.ECS;

namespace Swordfish.Graphics;

public struct MeshRendererComponent(in MeshRenderer meshRenderer) : IDataComponent
{
    public const int DefaultIndex = 3;

    public readonly MeshRenderer? MeshRenderer = meshRenderer;

    public bool Bound;
}
