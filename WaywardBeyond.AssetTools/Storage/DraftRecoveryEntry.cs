namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Recovered full-text draft entry paired with live disk modification state.
/// </summary>
public readonly record struct DraftRecoveryEntry(
    string DestinationPath,
    byte[] DraftBytes,
    string BaseSha256,
    string? CurrentDiskSha256,
    bool HasExternalModification,
    string SessionId,
    DateTimeOffset SavedUtc);
