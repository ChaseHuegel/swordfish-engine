using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderContext
{
    DataBinding<CameraEntity> MainCamera { get; }

    DataBinding<int> DrawCalls { get; }

    void Bind(Shader shader);

    void Bind(Texture texture);

    void Bind(Mesh mesh);

    void Bind(MeshRenderer meshRenderer, int entity);
    
    void Bind(RectRenderer rectRenderer);
}
