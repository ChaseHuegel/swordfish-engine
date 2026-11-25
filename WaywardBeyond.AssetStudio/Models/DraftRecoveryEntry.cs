namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Describes an unsaved draft file found after an unclean studio exit.
/// </summary>
public readonly record struct DraftRecoveryEntry(
    string RelativeFile,
    string SavedAtLabel,
    bool BaseHashChanged,
    string Summary,
    string? AbsoluteFilePath = null);
