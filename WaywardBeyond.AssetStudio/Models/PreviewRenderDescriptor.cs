namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes the active preview surface inputs consumed by the preview viewport control.
/// </summary>
public readonly record struct PreviewRenderDescriptor(
    PreviewSurfaceMode Mode,
    string Title,
    string Subtitle,
    string AccentHex,
    string ShapeName,
    BrickOrientation Orientation,
    bool HasMissingTexture,
    bool IsOrbiting,
    double OrbitAngleDegrees,
    string SpriteState,
    string SpriteDirection,
    string BlueprintDimensions,
    string? SpriteTexturePath = null,
    string? MaterialTexturePath = null,
    string? PaintTexturePath = null,
    bool IsTexturePaint = false,
    MaterialPreviewShape MaterialShape = MaterialPreviewShape.Sphere);
