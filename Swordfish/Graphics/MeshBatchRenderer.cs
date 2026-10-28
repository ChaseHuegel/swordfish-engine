using Swordfish.Library.Types;

namespace Swordfish.Graphics;

/// <summary>
/// Binds a <see cref="MeshBatch"/> to a material set and render state.
/// </summary>
public sealed class MeshBatchRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>True if this renderer has been bound to the render context.</summary>
    public bool IsBound { get; internal set; }

    public MeshBatch MeshBatch { get; }

    public Material[] Materials { get; }

    public RenderOptions RenderOptions { get; }

    public MeshBatchRenderer(MeshBatch meshBatch, params Material[] materials)
    {
        MeshBatch = meshBatch;
        Materials = materials;
    }

    public MeshBatchRenderer(MeshBatch meshBatch, Material[] materials, RenderOptions renderOptions) : this(meshBatch, materials)
    {
        RenderOptions = renderOptions;
    }

    public MeshBatchRenderer(MeshBatch meshBatch, Material material, RenderOptions renderOptions) : this(meshBatch, material)
    {
        RenderOptions = renderOptions;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
