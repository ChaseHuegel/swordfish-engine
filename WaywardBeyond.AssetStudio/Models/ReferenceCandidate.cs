namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Represents an option displayed in a searchable reference picker.
/// </summary>
public readonly record struct ReferenceCandidate(
    string Value,
    ReferenceKind Kind,
    AssetProvenance Provenance,
    ReferenceResolutionState ResolutionState,
    string SourceRoot,
    string Detail,
    string? ThumbnailPath = null)
{
    /// <summary>
    ///     Whether this candidate has a previewable thumbnail image path.
    /// </summary>
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailPath);

    /// <summary>
    ///     Short file or layer name shown beneath visual thumbnail tiles.
    /// </summary>
    public string DisplayFileName => string.IsNullOrWhiteSpace(Value)
        ? string.Empty
        : Path.GetFileName(Value.Replace('\\', '/'));
}
