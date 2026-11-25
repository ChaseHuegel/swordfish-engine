namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Captures user selections from the new asset dialog.
/// </summary>
public readonly record struct NewAssetRequest(
    AssetKind Kind,
    string AssetId,
    bool UseExistingFile,
    string TargetRelativeFile);
