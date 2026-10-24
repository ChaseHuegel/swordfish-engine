using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Components;

public struct BillboardComponent : IDataComponent
{
    /// <summary>Offset from the entity's transform, applied in world space.</summary>
    public Vector3 Offset;

    /// <summary>The quad's width and height in world units.</summary>
    public Vector2 Size;

    /// <summary>The materials drawn on the quad. Index 0 is the forward-facing material; additional materials are directional.</summary>
    public Material[] Materials;
}