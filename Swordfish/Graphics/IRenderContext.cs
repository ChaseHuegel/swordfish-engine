using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderContext
{
    DataBinding<Camera> Camera { get; set; }

    DataBinding<int> DrawCalls { get; }

    void RefreshRenderTargets();

    void Bind(Shader shader);

    void Bind(Texture texture);

    void Bind(Mesh mesh);

    void Bind(MeshRenderer meshRenderer);
}
