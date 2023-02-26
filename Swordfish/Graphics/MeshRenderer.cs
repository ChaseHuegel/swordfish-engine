using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public sealed class MeshRenderer : IHandle
{
    public event EventHandler<EventArgs>? Disposed;

    public Transform Transform { get; set; }

    public Mesh Mesh { get; set; }

    //  TODO create a Material object which encapsulates a Shader and collection of Textures
    public Shader Shader { get; set; }

    public MeshRenderer(Mesh mesh, Shader shader)
    {
        Mesh = mesh;
        Shader = shader;
    }

    public void Dispose()
    {
        Disposed?.Invoke(this, EventArgs.Empty);
    }
}
