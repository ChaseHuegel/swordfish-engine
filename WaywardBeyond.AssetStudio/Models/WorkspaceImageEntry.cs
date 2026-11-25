namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Represents a discovered image file inside an opened workspace for sprite and texture picking.
/// </summary>
public readonly record struct WorkspaceImageEntry(
    string RelativePath,
    string FileName,
    string AbsolutePath,
    string FolderHint);
