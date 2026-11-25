namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     One file record stored in a multi-file save recovery manifest.
/// </summary>
public readonly record struct RecoveryManifestEntry(
    string DestinationPath,
    bool ExistedBefore,
    string BackupPath);
