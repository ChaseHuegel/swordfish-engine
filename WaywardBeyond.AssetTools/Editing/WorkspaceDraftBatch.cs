using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Storage;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Undoable multi-file workspace draft batch produced by a rename or bulk refactor.
/// </summary>
public sealed class WorkspaceDraftBatch
{
    private readonly IReadOnlyList<RenameFilePlan> _files;
    private readonly Dictionary<string, byte[]?> _previousDraftBytes;

    /// <summary>
    ///     Creates an undoable draft batch recording prior draft state for every modified file.
    /// </summary>
    public WorkspaceDraftBatch(
        string description,
        IReadOnlyList<RenameFilePlan> files,
        Dictionary<string, byte[]?> previousDraftBytes)
    {
        Description = description;
        _files = files;
        _previousDraftBytes = previousDraftBytes;
    }

    /// <summary>
    ///     Gets the human-readable description of this workspace operation.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the ordered list of planned file changes in this batch.
    /// </summary>
    public IReadOnlyList<RenameFilePlan> Files => _files;

    /// <summary>
    ///     Gets a value indicating whether this batch is currently applied to drafts.
    /// </summary>
    public bool IsApplied { get; private set; }

    /// <summary>
    ///     Writes every updated document in this batch into <paramref name="draftStore"/>.
    /// </summary>
    public Result Apply(DraftStore draftStore)
    {
        if (draftStore == null)
        {
            return Result.FromFailure("DraftStore is required.");
        }

        for (int i = 0; i < _files.Count; i++)
        {
            RenameFilePlan file = _files[i];
            Result saved = draftStore.SaveDraft(file.FilePath, file.UpdatedBytes, file.BaseSha256);
            if (!saved.Success)
            {
                return saved;
            }
        }

        IsApplied = true;
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Reverts every file in this batch back to its prior draft state or discards newly created drafts.
    /// </summary>
    public Result Undo(DraftStore draftStore)
    {
        if (draftStore == null)
        {
            return Result.FromFailure("DraftStore is required.");
        }

        for (int i = _files.Count - 1; i >= 0; i--)
        {
            RenameFilePlan file = _files[i];
            if (_previousDraftBytes.TryGetValue(file.FilePath, out byte[]? priorBytes) && priorBytes != null)
            {
                Result restored = draftStore.SaveDraft(file.FilePath, priorBytes, file.BaseSha256);
                if (!restored.Success)
                {
                    return restored;
                }
            }
            else
            {
                Result discarded = draftStore.DiscardDraft(file.FilePath);
                if (!discarded.Success)
                {
                    return discarded;
                }
            }
        }

        IsApplied = false;
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Builds atomic multi-file save requests carrying each file's expected base SHA-256 hash.
    /// </summary>
    public IReadOnlyList<FileSaveRequest> BuildSaveRequests()
    {
        List<FileSaveRequest> requests = new(_files.Count);
        for (int i = 0; i < _files.Count; i++)
        {
            RenameFilePlan file = _files[i];
            requests.Add(new FileSaveRequest(file.FilePath, file.UpdatedBytes, ExpectedBaseSha256: file.BaseSha256));
        }

        return requests;
    }
}
