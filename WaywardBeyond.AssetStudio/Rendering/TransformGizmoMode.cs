namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Identifies the active 3D manipulation gizmo tool in the item model editor.</summary>
public enum TransformGizmoMode
{
    /// <summary>Move along X, Y, Z axes or XY, XZ, YZ planes.</summary>
    Translate,

    /// <summary>Rotate around X, Y, or Z axes.</summary>
    Rotate,

    /// <summary>Scale uniformly or along local X, Y, Z axes.</summary>
    Scale,
}
