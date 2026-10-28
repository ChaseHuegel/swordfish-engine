using Swordfish.ECS;

namespace Swordfish.Graphics;

/// <summary>Associates a mesh batch renderer with an entity.</summary>
public readonly struct MeshBatchRendererComponent(in MeshBatchRenderer renderer) : IDataComponent
{
    public readonly MeshBatchRenderer Renderer = renderer;
}
