using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public sealed class MeshRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    public RenderOptions RenderOptions { get; set; }

    public Transform Transform { get; } = new();

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
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
