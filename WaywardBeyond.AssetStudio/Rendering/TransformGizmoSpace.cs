namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Identifies the coordinate basis used for translate and rotate gizmo handles.</summary>
public enum TransformGizmoSpace
{
    /// <summary>Uses the item's own rotated local axes.</summary>
    Local,

    /// <summary>Uses world X, Y, Z axes.</summary>
    World,
}
