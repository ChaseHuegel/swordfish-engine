namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Metadata persisted alongside a full-text draft for recovery and external conflict checks.
/// </summary>
public readonly record struct DraftMetadata(
    string DestinationPath,
    string BaseSha256,
    string SessionId,
    DateTimeOffset SavedUtc);
