namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Outcome of deleting a standalone asset file or removing one entry from a multi-entry TOML file.
/// </summary>
public readonly record struct AssetDeleteResult(
    string FilePath,
    bool DeletedWholeFile,
    string? UpdatedText);
