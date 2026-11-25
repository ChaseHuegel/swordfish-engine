namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     One merged value row in the read-only effective tag order pane with provider attribution.
/// </summary>
public readonly record struct TagValueProvenance(
    string Value,
    string RootId,
    string RootName,
    string FilePath,
    bool IsDuplicate);
