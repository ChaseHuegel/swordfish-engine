namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents a single directional sprite frame cycled in the body inspector preview.
/// </summary>
public readonly record struct BodyPreviewFrameItem(
    string State,
    string Direction,
    string TexturePath,
    string? AbsolutePath,
    string SwatchHex);
