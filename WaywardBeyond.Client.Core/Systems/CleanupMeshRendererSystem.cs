using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class CleanupMeshRendererSystem : EntitySystem<MeshRendererCleanup, MeshRendererComponent>
{
    protected override void OnTick(float delta, DataStore store, int entity, ref MeshRendererCleanup meshRendererCleanup, ref MeshRendererComponent meshRendererComponent)
    {
        if (!meshRendererComponent.Bound)
        {
            return;
        }

        while (meshRendererCleanup.MeshRenderers.TryTake(out MeshRenderer? meshRenderer))
        {
            meshRenderer.Dispose();
            meshRenderer.Mesh.Dispose();
        }
    }
}