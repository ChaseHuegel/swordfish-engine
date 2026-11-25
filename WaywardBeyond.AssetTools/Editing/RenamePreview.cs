using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Complete preview of affected files, spans, and warnings for a cross-file asset or key rename.
/// </summary>
public readonly record struct RenamePreview(
    AssetKind Kind,
    string OldId,
    string NewId,
    IReadOnlyList<RenameFilePlan> FilePlans,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    ///     Gets the total number of span edits across all affected files.
    /// </summary>
    public int TotalEditCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < FilePlans.Count; i++)
            {
                total += FilePlans[i].Edits.Count;
            }

            return total;
        }
    }
}
