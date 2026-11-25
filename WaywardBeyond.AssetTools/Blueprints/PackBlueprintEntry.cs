namespace WaywardBeyond.AssetTools.Blueprints;

/// <summary>
///     Metadata summary for a pack blueprint <c>.wbp</c> file addressed by its relative path ID.
/// </summary>
public readonly record struct PackBlueprintEntry(
    string RelativeId,
    string FilePath,
    string Name,
    int SizeX,
    int SizeY,
    int SizeZ,
    int BrickCount,
    long CreatedAtMs,
    long ModifiedAtMs,
    long FileSizeBytes);
