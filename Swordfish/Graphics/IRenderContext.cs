using Swordfish.Graphics.SilkNET;

namespace Swordfish.Graphics;

public interface IRenderContext
{
    void Bind(Shader shader);

    void Bind(Texture texture);

    void Bind(Mesh mesh);

    void Bind(MeshRenderer meshRenderer);
}
