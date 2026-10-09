using System.Collections.Concurrent;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Components;

internal struct MeshRendererCleanup() : IDataComponent
{
    public readonly ConcurrentBag<MeshRenderer> MeshRenderers = [];
}