using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderTarget
{
    Transform Transform { get; set; }
    Mesh Mesh { get; set; }
    Shader Shader { get; set; }

    void Dispose();

    void Render(Camera camera);
}
