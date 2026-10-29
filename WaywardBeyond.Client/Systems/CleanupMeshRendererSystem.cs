using Swordfish.ECS;
using WaywardBeyond.Client.Components;

namespace WaywardBeyond.Client.Systems;

internal sealed class CleanupMeshRendererSystem : IEntitySystem
{
    private struct ForEachAction : IForEach<MeshRendererCleanup>
    {
        public void Execute(float delta, DataStore store, int entity, in MeshRendererCleanup meshRendererCleanup)
        {
            meshRendererCleanup.ProcessPending();
            meshRendererCleanup.DisposeMeshRenderers();
        }
    }

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = default;
        store.Query<MeshRendererCleanup, ForEachAction>(delta, ref action);
    }
}