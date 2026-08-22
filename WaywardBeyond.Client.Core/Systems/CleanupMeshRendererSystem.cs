using Swordfish.ECS;
using Swordfish.Graphics;
using WaywardBeyond.Client.Core.Components;

namespace WaywardBeyond.Client.Core.Systems;

internal sealed class CleanupMeshRendererSystem : IEntitySystem
{
    private struct ForEachAction : IForEach<MeshRendererCleanup, MeshRendererComponent>
    {
        public void Execute(float delta, DataStore store, int entity, in MeshRendererCleanup meshRendererCleanup, in MeshRendererComponent meshRendererComponent)
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

    public void Tick(float delta, DataStore store)
    {
        ForEachAction action = default;
        store.Query<MeshRendererCleanup, MeshRendererComponent, ForEachAction>(delta, ref action);
    }
}