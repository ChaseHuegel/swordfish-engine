namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes a ranked result row inside the command palette.
/// </summary>
public readonly record struct CommandPaletteEntry(
    string Title,
    string Subtitle,
    string Category,
    AssetKind? Kind,
    string? TargetAssetId,
    string? ActionId,
    AssetProvenance? Provenance,
    string ResolutionLabel,
    string? ShadowedFile,
    int Score);
