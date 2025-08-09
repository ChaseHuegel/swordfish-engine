using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Components;

internal struct MeshRendererCleanup(in MeshRenderer meshRenderer) : IDataComponent
{
    public readonly MeshRenderer MeshRenderer = meshRenderer;
}