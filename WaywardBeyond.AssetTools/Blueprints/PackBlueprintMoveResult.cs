namespace WaywardBeyond.AssetTools.Blueprints;

/// <summary>
///     Outcome of moving a pack blueprint file to a new relative path, including the pack ID change warning.
/// </summary>
public readonly record struct PackBlueprintMoveResult(
    PackBlueprintEntry Entry,
    string OldRelativeId,
    string NewRelativeId,
    string IdChangeNotice);
