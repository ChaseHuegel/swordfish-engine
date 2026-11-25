namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Represents a single reversible user edit stored on the studio undo stack.
/// </summary>
public sealed class StudioUndoAction
{
    /// <summary>
    ///     Initializes a reversible edit action with optional rapid-edit coalescing metadata.
    /// </summary>
    public StudioUndoAction(
        string description,
        string documentKey,
        Action undo,
        Action redo,
        string? coalesceKey = null,
        DateTimeOffset? timestampUtc = null)
    {
        Description = string.IsNullOrWhiteSpace(description) ? "Edit" : description;
        DocumentKey = documentKey ?? string.Empty;
        Undo = undo ?? throw new ArgumentNullException(nameof(undo));
        Redo = redo ?? throw new ArgumentNullException(nameof(redo));
        CoalesceKey = string.IsNullOrWhiteSpace(coalesceKey) ? null : coalesceKey;
        TimestampUtc = timestampUtc ?? DateTimeOffset.UtcNow;
    }

    /// <summary>
    ///     Human-readable label describing the reversible edit.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Identifier of the asset document affected by this edit.
    /// </summary>
    public string DocumentKey { get; }

    /// <summary>
    ///     Optional key coalescing consecutive rapid edits to the same field.
    /// </summary>
    public string? CoalesceKey { get; }

    /// <summary>
    ///     UTC timestamp of the latest edit merged into this entry.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; internal set; }

    /// <summary>
    ///     Callback restoring the document state prior to the edit.
    /// </summary>
    public Action Undo { get; }

    /// <summary>
    ///     Callback re-applying the edited document state.
    /// </summary>
    public Action Redo { get; internal set; }
}
