using Swordfish.Graphics.SilkNET;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderContext
{
    DataBinding<int> DrawCalls { get; }

    DataBinding<bool> Wireframe { get; set; }

    void RefreshRenderTargets();

    void Bind(Shader shader);

    void Bind(Texture texture);

    void Bind(Mesh mesh);

    void Bind(MeshRenderer meshRenderer);
}
