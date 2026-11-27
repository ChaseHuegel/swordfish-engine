namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Selects the visual surface rendered inside the inspector preview card.
/// </summary>
public enum PreviewSurfaceMode
{
    /// <summary>
    ///     Single-shape brick preview with orbit and orientation controls.
    /// </summary>
    SingleBrick,

    /// <summary>
    ///     Multi-shape labeled grid preview for bricks with Shape = Any.
    /// </summary>
    BrickShapeGrid,

    /// <summary>
    ///     Connected-texture brick showcase exhibiting all 16 cardinal adjacency masks.
    /// </summary>
    BrickConnectedShowcase,

    /// <summary>
    ///     First-person held view-model framing preset for items.
    /// </summary>
    ItemViewModel,

    /// <summary>
    ///     Unit-cube world-model framing preset for items.
    /// </summary>
    ItemWorldModel,

    /// <summary>
    ///     Color or texture paint swatch preview.
    /// </summary>
    PaintSwatch,

    /// <summary>
    ///     2D directional character body sprite preview.
    /// </summary>
    BodySprite,

    /// <summary>
    ///     Unlit material surface preview with alpha cutoff.
    /// </summary>
    MaterialPreview,

    /// <summary>
    ///     Multi-voxel blueprint cluster preview.
    /// </summary>
    BlueprintVoxel,

    /// <summary>
    ///     Placeholder surface for non-3D data documents.
    /// </summary>
    DataDocument,

    /// <summary>
    ///     Interactive crafting flow diagram preview for recipes.
    /// </summary>
    RecipeFlow,
}
