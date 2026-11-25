namespace WaywardBeyond.AssetTools.Storage;

/// <summary>
///     Describes why a file on disk conflicted with the expected base revision during save.
/// </summary>
public enum FileSaveConflictReason
{
    /// <summary>
    ///     The file on disk was modified externally after editing began.
    /// </summary>
    ModifiedExternally = 0,

    /// <summary>
    ///     The file on disk was deleted externally after editing began.
    /// </summary>
    DeletedExternally = 1,

    /// <summary>
    ///     A file was created externally at the target path before saving a new document.
    /// </summary>
    CreatedExternally = 2
}
