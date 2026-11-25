namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Parameters for writing one file atomically with optional external-change verification.
/// </summary>
public readonly record struct FileSaveRequest(
    string DestinationPath,
    byte[] Content,
    string? ExpectedBaseSha256 = null,
    bool ExpectFileNotToExist = false);
