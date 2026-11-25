using WaywardBeyond.AssetStudio.Rendering;
using WaywardBeyond.Bricks;
using WaywardBeyond.Meshing;

namespace WaywardBeyond.AssetStudio.Models;

/// <summary>Resolved CPU geometry, texture mode, and source metadata for previewing or editing an item model.</summary>
public readonly record struct ResolvedItemModelVisual(
    VoxelMesh Geometry,
    PreviewTextureMode TextureMode,
    BrickAlpha AlphaMode,
    float AlphaCutoff,
    bool DoubleSided,
    StandaloneTextureData? StandaloneTexture,
    bool IsPlaceableBrickAny,
    bool IsPlaceholder,
    string SourceLabel,
    string? AccentHex);
