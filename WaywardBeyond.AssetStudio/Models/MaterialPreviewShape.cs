namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Selects the 3D geometry primitive used to preview material assets.
/// </summary>
public enum MaterialPreviewShape
{
    /// <summary>
    ///     Smooth UV sphere primitive.
    /// </summary>
    Sphere = 0,

    /// <summary>
    ///     Centered upright double-sided quad plane.
    /// </summary>
    Plane = 1,

    /// <summary>
    ///     Unit cube primitive.
    /// </summary>
    Cube = 2,
}
