namespace WaywardBeyond.AssetStudio.Rendering;

/// <summary>Identifies which 3D gizmo axis or plane handle is hovered or actively dragged.</summary>
public enum TransformGizmoAxis
{
    /// <summary>No handle targeted.</summary>
    None,

    /// <summary>X axis handle (red).</summary>
    X,

    /// <summary>Y axis handle (green).</summary>
    Y,

    /// <summary>Z axis handle (blue).</summary>
    Z,

    /// <summary>XY planar translation quad.</summary>
    XY,

    /// <summary>XZ planar translation quad.</summary>
    XZ,

    /// <summary>YZ planar translation quad.</summary>
    YZ,

    /// <summary>Center uniform scale handle.</summary>
    Uniform,
}
