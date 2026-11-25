namespace WaywardBeyond.AssetStudio.Services;

/// <summary>
///     Maintains a bounded application-wide undo and redo history with rapid-edit coalescing.
/// </summary>
public sealed class StudioUndoStack
{
    /// <summary>
    ///     Default maximum number of undo entries retained in memory.
    /// </summary>
    public const int DEFAULT_CAPACITY = 200;

    /// <summary>
    ///     Default time window for coalescing consecutive edits with the same coalesce key.
    /// </summary>
    public static readonly TimeSpan DEFAULT_COALESCE_WINDOW = TimeSpan.FromMilliseconds(650);

    private readonly int _capacity;
    private readonly TimeSpan _coalesceWindow;
    private readonly List<StudioUndoAction> _undoEntries = [];
    private readonly List<StudioUndoAction> _redoEntries = [];

    /// <summary>
    ///     Initializes an undo stack with the specified entry capacity and coalesce window.
    /// </summary>
    public StudioUndoStack(int capacity = DEFAULT_CAPACITY, TimeSpan? coalesceWindow = null)
    {
        _capacity = Math.Max(1, capacity);
        _coalesceWindow = coalesceWindow ?? DEFAULT_COALESCE_WINDOW;
    }

    /// <summary>
    ///     Raised whenever the undo or redo stack state changes.
    /// </summary>
    public event Action? StateChanged;

    /// <summary>
    ///     Gets whether at least one edit can be undone.
    /// </summary>
    public bool CanUndo => _undoEntries.Count > 0;

    /// <summary>
    ///     Gets whether at least one undone edit can be redone.
    /// </summary>
    public bool CanRedo => _redoEntries.Count > 0;

    /// <summary>
    ///     Gets the number of entries currently on the undo stack.
    /// </summary>
    public int UndoCount => _undoEntries.Count;

    /// <summary>
    ///     Gets the number of entries currently on the redo stack.
    /// </summary>
    public int RedoCount => _redoEntries.Count;

    /// <summary>
    ///     Gets the description of the next action to undo, or null when empty.
    /// </summary>
    public string? UndoDescription => _undoEntries.Count > 0 ? _undoEntries[^1].Description : null;

    /// <summary>
    ///     Gets the description of the next action to redo, or null when empty.
    /// </summary>
    public string? RedoDescription => _redoEntries.Count > 0 ? _redoEntries[^1].Description : null;

    /// <summary>
    ///     Gets whether an undo or redo callback is currently executing so listeners avoid re-recording edits.
    /// </summary>
    public bool IsReplaying { get; private set; }

    /// <summary>
    ///     Records a new reversible action, coalescing with the previous entry when keys match within the coalesce window.
    /// </summary>
    public void Push(StudioUndoAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (IsReplaying)
        {
            return;
        }

        _redoEntries.Clear();

        if (_undoEntries.Count > 0)
        {
            var previous = _undoEntries[^1];
            if (CanCoalesce(previous, action))
            {
                previous.Redo = action.Redo;
                previous.TimestampUtc = action.TimestampUtc;
                StateChanged?.Invoke();
                return;
            }
        }

        _undoEntries.Add(action);
        while (_undoEntries.Count > _capacity)
        {
            _undoEntries.RemoveAt(0);
        }

        StateChanged?.Invoke();
    }

    /// <summary>
    ///     Undoes the most recent action and moves it to the redo stack.
    /// </summary>
    public bool Undo()
    {
        if (_undoEntries.Count == 0 || IsReplaying)
        {
            return false;
        }

        var action = _undoEntries[^1];
        _undoEntries.RemoveAt(_undoEntries.Count - 1);
        _redoEntries.Add(action);

        IsReplaying = true;
        try
        {
            action.Undo();
        }
        finally
        {
            IsReplaying = false;
        }

        StateChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Re-applies the most recently undone action and moves it back to the undo stack.
    /// </summary>
    public bool Redo()
    {
        if (_redoEntries.Count == 0 || IsReplaying)
        {
            return false;
        }

        var action = _redoEntries[^1];
        _redoEntries.RemoveAt(_redoEntries.Count - 1);
        _undoEntries.Add(action);
        while (_undoEntries.Count > _capacity)
        {
            _undoEntries.RemoveAt(0);
        }

        IsReplaying = true;
        try
        {
            action.Redo();
        }
        finally
        {
            IsReplaying = false;
        }

        StateChanged?.Invoke();
        return true;
    }

    /// <summary>
    ///     Removes all undo and redo entries associated with <paramref name="documentKey"/>.
    /// </summary>
    public void RemoveForDocument(string documentKey)
    {
        if (string.IsNullOrEmpty(documentKey))
        {
            return;
        }

        var removedUndo = _undoEntries.RemoveAll(entry => string.Equals(entry.DocumentKey, documentKey, StringComparison.Ordinal));
        var removedRedo = _redoEntries.RemoveAll(entry => string.Equals(entry.DocumentKey, documentKey, StringComparison.Ordinal));
        if (removedUndo > 0 || removedRedo > 0)
        {
            StateChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Clears all recorded undo and redo entries.
    /// </summary>
    public void Clear()
    {
        if (_undoEntries.Count == 0 && _redoEntries.Count == 0)
        {
            return;
        }

        _undoEntries.Clear();
        _redoEntries.Clear();
        StateChanged?.Invoke();
    }

    private bool CanCoalesce(StudioUndoAction previous, StudioUndoAction next)
    {
        if (string.IsNullOrEmpty(previous.CoalesceKey) || string.IsNullOrEmpty(next.CoalesceKey))
        {
            return false;
        }

        if (!string.Equals(previous.CoalesceKey, next.CoalesceKey, StringComparison.Ordinal)
            || !string.Equals(previous.DocumentKey, next.DocumentKey, StringComparison.Ordinal))
        {
            return false;
        }

        var elapsed = next.TimestampUtc - previous.TimestampUtc;
        return elapsed >= TimeSpan.Zero && elapsed <= _coalesceWindow;
    }
}
