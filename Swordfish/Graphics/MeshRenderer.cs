using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public sealed class MeshRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    private volatile bool _isBound;

    /// <inheritdoc/>
    public bool IsDisposed { get; private set; }

    /// <summary>True if this renderer has been bound to the render context.</summary>
    public bool IsBound
    {
        get => _isBound;
        internal set => _isBound = value;
    }

    /// <summary>The bound render target, or null before binding.</summary>
    public IRenderTarget? RenderTarget { get; internal set; }

    public RenderOptions RenderOptions { get; set; } = new();

    public Mesh Mesh { get; }

    public Material[] Materials { get; }

    public MeshRenderer(Mesh mesh, params Material[] materials)
    {
        Mesh = mesh;
        Materials = materials;
    }

    public MeshRenderer(Mesh mesh, Material[] materials, RenderOptions renderOptions) : this(mesh, materials)
    {
        RenderOptions = renderOptions;
    }

    public MeshRenderer(Mesh mesh, Material material, RenderOptions renderOptions) : this(mesh, material)
    {
        RenderOptions = renderOptions;
    }

    public void Dispose()
    {
        if (IsDisposed)
        {
            return;
        }

        IsDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        GC.SuppressFinalize(this);
    }
}
