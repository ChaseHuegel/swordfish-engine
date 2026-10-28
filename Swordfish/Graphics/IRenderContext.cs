using Swordfish.ECS;
using Swordfish.Library.Types;

namespace Swordfish.Graphics;

public interface IRenderContext
{
    DataBinding<CameraEntity> MainCamera { get; }
    
    internal RenderScene GetSceneContext();

    internal bool TryDequeueUnbound(out UnboundRenderer unbound);

    internal bool TryDequeueUnboundBatch(out UnboundBatchRenderer unbound);

    void Bind(Shader shader);

    void Bind(Texture texture);

    void Bind(Mesh mesh);

    void Bind(MeshRenderer meshRenderer, Uuid entity);

    void Bind(MeshBatchRenderer meshBatchRenderer, Uuid entity);
    
    void Bind(RectRenderer rectRenderer);
    
    void Bind(Material material);
}
