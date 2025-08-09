using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class CleanupMeshRendererSystem : EntitySystem<MeshRendererCleanup>
{
    protected override void OnTick(float delta, DataStore store, int entity, ref MeshRendererCleanup meshRendererCleanup)
    {
        meshRendererCleanup.MeshRenderer.Dispose();
        store.Remove<MeshRendererCleanup>(entity);
    }
}