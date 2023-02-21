using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public class MeshRenderer
{
    public Transform Transform { get; set; }

    public Mesh Mesh { get; set; }

    //  TODO create a Material object which encapsulates a Shader and collection of Textures
    public Shader Shader { get; set; }

    internal IHandle? RenderContext;
    internal IHandle? Handle;

    public MeshRenderer(Mesh mesh, Shader shader)
    {
        Mesh = mesh;
        Shader = shader;
    }

    public void Dispose()
    {
        Mesh.Handle?.Dispose();
        Shader.Handle?.Dispose();
        //  TODO Remove self from RenderContext
    }
}
