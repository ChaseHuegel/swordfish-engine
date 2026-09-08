using System.Numerics;
using Swordfish.ECS;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.Core.Components;

/// <summary>
/// Marks an entity to be rendered as a camera-facing textured plane (a billboard). A general, reusable
/// marker/data component — not player-specific. Any entity (player, NPC, prop) carrying this is rendered
/// by <see cref="WaywardBeyond.Client.Core.Systems.BillboardSystem"/> as a standalone quad that tracks the
/// owning <see cref="Parent"/> entity's position and always faces the camera.
/// </summary>
public struct BillboardComponent : IDataComponent
{
    /// <summary>The entity whose world position the billboard tracks (the visual "owner").</summary>
    public Uuid Parent;

    /// <summary>Local offset from the owner's transform, applied in world space.</summary>
    public Vector3 Offset;

    /// <summary>The quad's width and height in world units.</summary>
    public Vector2 Size;

    /// <summary>The material drawn on the quad.</summary>
    public Material Material;
}