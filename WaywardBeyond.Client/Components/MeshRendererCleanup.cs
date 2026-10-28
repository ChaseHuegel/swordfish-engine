using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Components;

internal struct MeshRendererCleanup() : IDataComponent
{
    public readonly ConcurrentBag<MeshRenderer> MeshRenderers = [];
    public readonly ConcurrentQueue<PendingEntry> Pending = [];

    internal sealed class PendingEntry(MeshRenderer oldRenderer, MeshRenderer? newRenderer)
    {
        public readonly MeshRenderer OldRenderer = oldRenderer;
        public readonly MeshRenderer? NewRenderer = newRenderer;
        public int Age;
    }

    /// <summary>Disposes every queued renderer and its mesh.</summary>
    public void DisposeMeshRenderers()
    {
        while (MeshRenderers.TryTake(out MeshRenderer? meshRenderer))
        {
            meshRenderer.Dispose();
            meshRenderer.Mesh.Dispose();
        }
    }

    /// <summary>Disposes every queued and pending renderer and its mesh.</summary>
    public void DisposeAll()
    {
        DisposeMeshRenderers();
        while (Pending.TryDequeue(out PendingEntry? pending))
        {
            pending.OldRenderer.Dispose();
            pending.OldRenderer.Mesh.Dispose();
        }
    }

    /// <summary>Processes pending replacements and disposes old renderers whose replacements are bound.</summary>
    public void ProcessPending()
    {
        int count = Pending.Count;
        for (var i = 0; i < count; i++)
        {
            if (!Pending.TryDequeue(out PendingEntry? pending))
            {
                break;
            }

            pending.Age++;

            if ((pending.NewRenderer != null && pending.NewRenderer.IsBound)
                || (pending.NewRenderer == null && pending.Age >= 2)
                || (pending.NewRenderer != null && pending.NewRenderer.IsDisposed)
                || pending.Age >= 5)
            {
                pending.OldRenderer.Dispose();
                pending.OldRenderer.Mesh.Dispose();
            }
            else
            {
                Pending.Enqueue(pending);
            }
        }
    }
}