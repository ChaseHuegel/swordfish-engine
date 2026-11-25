using Swordfish.Library.Util;
using WaywardBeyond.AssetTools.Documents;
using WaywardBeyond.AssetTools.Workspace;

namespace WaywardBeyond.AssetTools.Editing;

/// <summary>
///     Two-pane tag editor model maintaining editable file order, live merged effective order, and undo/redo history.
/// </summary>
public sealed class TagDocumentModel
{
    private readonly string _ownerRootId;
    private readonly string _ownerRootName;
    private readonly IReadOnlyList<IndexedTagEntry> _contributingEntries;
    private readonly List<string> _fileOrder;
    private readonly List<List<string>> _undoStack = [];
    private readonly List<List<string>> _redoStack = [];
    private readonly List<TagValueProvenance> _effectiveOrder = [];
    private readonly HashSet<string> _duplicateValues = new(StringComparer.Ordinal);

    /// <summary>
    ///     Creates a two-pane tag editor model for <paramref name="targetEntry"/> merged with <paramref name="allTagEntries"/>.
    /// </summary>
    public TagDocumentModel(in IndexedTagEntry targetEntry, IReadOnlyList<IndexedTagEntry> allTagEntries)
    {
        TagName = targetEntry.TagName;
        Locale = targetEntry.Locale;
        FilePath = targetEntry.FilePath;
        IsReadOnly = !targetEntry.IsWritable;
        _ownerRootId = targetEntry.RootId;
        _ownerRootName = targetEntry.RootName;
        _fileOrder = [.. targetEntry.Values];

        List<IndexedTagEntry> contributors = [];
        for (int i = 0; i < allTagEntries.Count; i++)
        {
            IndexedTagEntry entry = allTagEntries[i];
            if (!string.Equals(entry.TagName, TagName, StringComparison.Ordinal))
            {
                continue;
            }

            bool isInvariant = string.IsNullOrEmpty(entry.Locale);
            bool matchesLocale = string.Equals(entry.Locale, Locale, StringComparison.OrdinalIgnoreCase);
            if (isInvariant || matchesLocale)
            {
                contributors.Add(entry);
            }
        }

        _contributingEntries = contributors;
        RebuildEffectiveOrder();
    }

    /// <summary>
    ///     Gets the tag key edited by this model.
    /// </summary>
    public string TagName { get; }

    /// <summary>
    ///     Gets the tag file locale, or empty string for invariant tags.
    /// </summary>
    public string Locale { get; }

    /// <summary>
    ///     Gets the full path of the backing tag TOML file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    ///     Gets a value indicating whether the backing tag file belongs to a read-only reference root.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    ///     Gets the editable list of values stored in the current file.
    /// </summary>
    public IReadOnlyList<string> FileOrder => _fileOrder;

    /// <summary>
    ///     Gets the live read-only merged effective order across all contributing workspace roots.
    /// </summary>
    public IReadOnlyList<TagValueProvenance> EffectiveOrder => _effectiveOrder;

    /// <summary>
    ///     Gets the set of values that appear more than once in the merged effective list without auto-merging.
    /// </summary>
    public IReadOnlySet<string> DuplicateValues => _duplicateValues;

    /// <summary>
    ///     Gets a value indicating whether a semantic-order reorder warning has been triggered.
    /// </summary>
    public bool HasReorderWarning { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether an edit can be undone.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether an undone edit can be redone.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    ///     Appends <paramref name="value"/> to the end of the file's tag list as one undoable operation.
    /// </summary>
    public Result Append(string value)
    {
        if (IsReadOnly)
        {
            return Result.FromFailure("Tag file belongs to a read-only reference root.");
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            return Result.FromFailure("Tag value cannot be empty.");
        }

        PushUndoSnapshot();
        _fileOrder.Add(value.Trim());
        RebuildEffectiveOrder();
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Reorders a tag value from <paramref name="fromIndex"/> to <paramref name="toIndex"/> and flags the semantic-order warning once.
    /// </summary>
    public Result<bool> Reorder(int fromIndex, int toIndex)
    {
        if (IsReadOnly)
        {
            return Result<bool>.FromFailure("Tag file belongs to a read-only reference root.");
        }

        if (fromIndex < 0 || fromIndex >= _fileOrder.Count || toIndex < 0 || toIndex >= _fileOrder.Count)
        {
            return Result<bool>.FromFailure("Reorder indices are out of range.");
        }

        if (fromIndex == toIndex)
        {
            return Result<bool>.FromSuccess(false);
        }

        PushUndoSnapshot();
        string item = _fileOrder[fromIndex];
        _fileOrder.RemoveAt(fromIndex);
        _fileOrder.Insert(toIndex, item);

        bool shouldWarnNow = !HasReorderWarning;
        HasReorderWarning = true;
        RebuildEffectiveOrder();
        return Result<bool>.FromSuccess(shouldWarnNow);
    }

    /// <summary>
    ///     Deletes the tag value at <paramref name="index"/> as one undoable operation.
    /// </summary>
    public Result Delete(int index)
    {
        if (IsReadOnly)
        {
            return Result.FromFailure("Tag file belongs to a read-only reference root.");
        }

        if (index < 0 || index >= _fileOrder.Count)
        {
            return Result.FromFailure("Delete index is out of range.");
        }

        PushUndoSnapshot();
        _fileOrder.RemoveAt(index);
        RebuildEffectiveOrder();
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Undoes the most recent tag list edit and refreshes the live effective pane.
    /// </summary>
    public Result Undo()
    {
        if (_undoStack.Count == 0)
        {
            return Result.FromFailure("Nothing to undo.");
        }

        _redoStack.Add([.. _fileOrder]);
        int last = _undoStack.Count - 1;
        List<string> snapshot = _undoStack[last];
        _undoStack.RemoveAt(last);

        _fileOrder.Clear();
        _fileOrder.AddRange(snapshot);
        RebuildEffectiveOrder();
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Redoes the most recently undone tag list edit and refreshes the live effective pane.
    /// </summary>
    public Result Redo()
    {
        if (_redoStack.Count == 0)
        {
            return Result.FromFailure("Nothing to redo.");
        }

        _undoStack.Add([.. _fileOrder]);
        int last = _redoStack.Count - 1;
        List<string> snapshot = _redoStack[last];
        _redoStack.RemoveAt(last);

        _fileOrder.Clear();
        _fileOrder.AddRange(snapshot);
        RebuildEffectiveOrder();
        return Result.FromSuccess();
    }

    /// <summary>
    ///     Writes the current editable file order back to the matching <c>[Tags]</c> array in <paramref name="document"/>.
    /// </summary>
    public Result ApplyToDocument(TomlDocument document)
    {
        if (IsReadOnly)
        {
            return Result.FromFailure("Tag file belongs to a read-only reference root.");
        }

        if (document == null)
        {
            return Result.FromFailure("Document is required.");
        }

        Result<TomlFieldSpan> field = document.GetField("Tags", TagName);
        if (!field.Success)
        {
            return Result.FromFailure(field.Message);
        }

        return document.SetStringArray(field.Value, _fileOrder);
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Add([.. _fileOrder]);
        _redoStack.Clear();
    }

    private void RebuildEffectiveOrder()
    {
        _effectiveOrder.Clear();
        _duplicateValues.Clear();

        List<(string Value, string RootId, string RootName, string Path)> raw = [];
        bool replacedCurrent = false;

        for (int i = 0; i < _contributingEntries.Count; i++)
        {
            IndexedTagEntry entry = _contributingEntries[i];
            bool isTarget = string.Equals(entry.FilePath, FilePath, StringComparison.OrdinalIgnoreCase);
            IReadOnlyList<string> values = isTarget ? _fileOrder : entry.Values;
            if (isTarget)
            {
                replacedCurrent = true;
            }

            for (int j = 0; j < values.Count; j++)
            {
                raw.Add((values[j], entry.RootId, entry.RootName, entry.FilePath));
            }
        }

        if (!replacedCurrent)
        {
            for (int j = 0; j < _fileOrder.Count; j++)
            {
                raw.Add((_fileOrder[j], _ownerRootId, _ownerRootName, FilePath));
            }
        }

        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        for (int i = 0; i < raw.Count; i++)
        {
            string val = raw[i].Value;
            counts[val] = counts.TryGetValue(val, out int c) ? c + 1 : 1;
        }

        foreach (KeyValuePair<string, int> kvp in counts)
        {
            if (kvp.Value > 1)
            {
                _duplicateValues.Add(kvp.Key);
            }
        }

        for (int i = 0; i < raw.Count; i++)
        {
            (string val, string rootId, string rootName, string path) = raw[i];
            _effectiveOrder.Add(new TagValueProvenance(val, rootId, rootName, path, _duplicateValues.Contains(val)));
        }
    }
}
