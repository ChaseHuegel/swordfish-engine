namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Captures user selections from the pack export dialog.
/// </summary>
public readonly record struct ExportPackRequest(
    string PackId,
    int ContentVersion,
    string ArchiveFormat,
    bool AcknowledgeWarnings);
