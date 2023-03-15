using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public sealed class MeshRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    public Transform Transform { get; set; }

    public Mesh Mesh { get; }

    public Material[] Materials { get; }

    public MeshRenderer(Mesh mesh, params Material[] materials)
    {
        Mesh = mesh;
        Materials = materials;
        Transform = new Transform();
    }

    public void Dispose()
    {
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
