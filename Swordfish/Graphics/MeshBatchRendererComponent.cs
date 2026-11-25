using Krill;

namespace Swordfish.Graphics;

/// <summary>Associates a mesh batch renderer with an entity.</summary>
public readonly struct MeshBatchRendererComponent(in MeshBatchRenderer renderer) : IDataComponent, IDisposable
{
    public readonly MeshBatchRenderer Renderer = renderer;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Renderer == null)
        {
            return;
        }

        Renderer.Dispose();
        Renderer.MeshBatch.Dispose();
    }
}
