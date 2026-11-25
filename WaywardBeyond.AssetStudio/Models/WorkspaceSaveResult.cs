namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Reports the outcome of an atomic multi-file workspace save operation.
/// </summary>
public readonly record struct WorkspaceSaveResult(
    bool Success,
    int SavedFileCount,
    string Message,
    IReadOnlyDictionary<string, string>? UpdatedFileHashes = null,
    IReadOnlyDictionary<string, string>? UpdatedFileTexts = null);
