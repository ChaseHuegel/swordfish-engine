using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Components;

internal struct MeshRendererCleanup() : IDataComponent
{
    public readonly ConcurrentBag<MeshRenderer> MeshRenderers = [];

    /// <summary>Disposes every queued renderer and its mesh.</summary>
    public void DisposeMeshRenderers()
    {
        while (MeshRenderers.TryTake(out MeshRenderer? meshRenderer))
        {
            meshRenderer.Dispose();
            meshRenderer.Mesh.Dispose();
        }
    }
}