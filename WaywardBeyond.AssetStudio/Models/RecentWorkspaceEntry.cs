namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes a previously opened workspace displayed on the start screen.
/// </summary>
public readonly record struct RecentWorkspaceEntry(
    string Name,
    WorkspaceMode Mode,
    string RootPath,
    string LastOpenedLabel,
    int AssetCount,
    bool HasPackMetadata);
