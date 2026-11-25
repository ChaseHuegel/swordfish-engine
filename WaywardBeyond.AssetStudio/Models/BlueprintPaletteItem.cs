namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Represents one brick or paint entry referenced in a blueprint palette together with its usage count.
/// </summary>
public readonly record struct BlueprintPaletteItem(
    string AssetId,
    AssetKind Kind,
    int VoxelCount,
    bool IsResolved,
    string SummaryLabel);
