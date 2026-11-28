using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable <c>[[SlotGroups.Slots]]</c> per-slot filter override row.
/// </summary>
public sealed partial class SlotFilterRowViewModel : ObservableObject
{
    private readonly Action<SlotFilterRowViewModel>? _onChanged;
    private readonly Action<SlotFilterRowViewModel, int, int>? _onIndexTransition;
    private readonly Action<SlotFilterRowViewModel>? _onAddTag;
    private readonly Action<SlotFilterRowViewModel, SlotTagRowViewModel>? _onRemoveTag;
    private int _index;
    private string _indexText;
    private bool _isReadOnly;
    private bool _isIndexOutOfRange;
    private bool _isDuplicateIndex;

    /// <summary>
    ///     Initializes a per-slot filter override row for a slot group definition.
    /// </summary>
    public SlotFilterRowViewModel(
        int index,
        bool isReadOnly = false,
        Action<SlotFilterRowViewModel>? onChanged = null,
        Action<SlotFilterRowViewModel, int, int>? onIndexTransition = null,
        Action<SlotFilterRowViewModel>? onAddTag = null,
        Action<SlotFilterRowViewModel, SlotTagRowViewModel>? onRemoveTag = null)
    {
        _index = Math.Max(0, index);
        _indexText = _index.ToString(CultureInfo.InvariantCulture);
        _isReadOnly = isReadOnly;
        _onChanged = onChanged;
        _onIndexTransition = onIndexTransition;
        _onAddTag = onAddTag;
        _onRemoveTag = onRemoveTag;
    }

    /// <summary>
    ///     Zero-based local slot index within the owning slot group.
    /// </summary>
    public int Index
    {
        get => _index;
        set
        {
            var safe = Math.Max(0, value);
            var oldValue = _index;
            if (!SetProperty(ref _index, safe))
            {
                return;
            }

            SetProperty(ref _indexText, safe.ToString(CultureInfo.InvariantCulture), nameof(IndexText));
            OnPropertyChanged(nameof(IndexLabel));
            _onIndexTransition?.Invoke(this, oldValue, safe);
            _onChanged?.Invoke(this);
        }
    }

    /// <summary>
    ///     Editable string representation of <see cref="Index"/> bound to numeric text input.
    /// </summary>
    public string IndexText
    {
        get => _indexText;
        set
        {
            var safe = value ?? string.Empty;
            if (!SetProperty(ref _indexText, safe))
            {
                return;
            }

            if (int.TryParse(safe.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                Index = parsed;
            }
        }
    }

    /// <summary>
    ///     Whether editing controls on this slot override row are disabled.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            if (SetProperty(ref _isReadOnly, value))
            {
                OnPropertyChanged(nameof(CanEdit));
                for (var i = 0; i < AllowedTags.Count; i++)
                {
                    AllowedTags[i].SetReadOnly(value);
                }
            }
        }
    }

    /// <summary>
    ///     Whether editing controls on this row are enabled.
    /// </summary>
    public bool CanEdit => !IsReadOnly;

    /// <summary>
    ///     Editable invariant item tag references allowed in this specific slot.
    /// </summary>
    public ObservableCollection<SlotTagRowViewModel> AllowedTags { get; } = [];

    /// <summary>
    ///     Formatted display label for headers and preview cards.
    /// </summary>
    public string IndexLabel => $"Slot #{Index}";

    /// <summary>
    ///     Whether this slot override currently has no allowed tags configured.
    /// </summary>
    public bool HasNoAllowedTags => AllowedTags.Count == 0;

    /// <summary>
    ///     Summary count of configured tags on this slot override.
    /// </summary>
    public string TagsSummary => AllowedTags.Count == 0
        ? "No tags (unrestricted)"
        : (AllowedTags.Count == 1 ? "1 tag" : $"{AllowedTags.Count} tags");

    /// <summary>
    ///     Whether <see cref="Index"/> is greater than or equal to the owning group's capacity.
    /// </summary>
    public bool IsIndexOutOfRange
    {
        get => _isIndexOutOfRange;
        private set
        {
            if (SetProperty(ref _isIndexOutOfRange, value))
            {
                OnPropertyChanged(nameof(HasIndexWarning));
                OnPropertyChanged(nameof(IndexWarningLabel));
            }
        }
    }

    /// <summary>
    ///     Whether another slot override in the same group declares the same <see cref="Index"/>.
    /// </summary>
    public bool IsDuplicateIndex
    {
        get => _isDuplicateIndex;
        private set
        {
            if (SetProperty(ref _isDuplicateIndex, value))
            {
                OnPropertyChanged(nameof(HasIndexWarning));
                OnPropertyChanged(nameof(IndexWarningLabel));
            }
        }
    }

    /// <summary>
    ///     Whether this slot override has an out-of-range or duplicate index warning.
    /// </summary>
    public bool HasIndexWarning => IsIndexOutOfRange || IsDuplicateIndex;

    /// <summary>
    ///     Short diagnostic badge label for index validation issues.
    /// </summary>
    public string IndexWarningLabel => IsDuplicateIndex
        ? "Duplicate index"
        : (IsIndexOutOfRange ? "Out of range" : string.Empty);

    /// <summary>
    ///     Updates out-of-range and duplicate index flags from the owning slot group document.
    /// </summary>
    public void SetIndexValidation(bool isOutOfRange, bool isDuplicate)
    {
        IsIndexOutOfRange = isOutOfRange;
        IsDuplicateIndex = isDuplicate;
    }

    /// <summary>
    ///     Notifies tag collection summary bindings after tags are added or removed.
    /// </summary>
    public void NotifyTagsChanged()
    {
        OnPropertyChanged(nameof(HasNoAllowedTags));
        OnPropertyChanged(nameof(TagsSummary));
    }

    /// <summary>
    ///     Refreshes tag resolution across all allowed tags in this slot override.
    /// </summary>
    public void RefreshResolution(IReadOnlyList<ReferenceCandidate> candidates)
    {
        for (var i = 0; i < AllowedTags.Count; i++)
        {
            AllowedTags[i].RefreshResolution(candidates);
        }
    }

    /// <summary>
    ///     Restores <see cref="Index"/> during undo or redo replay without recording another edit.
    /// </summary>
    public void ApplyIndexForReplay(int restoredIndex)
    {
        var safe = Math.Max(0, restoredIndex);
        SetProperty(ref _index, safe, nameof(Index));
        SetProperty(ref _indexText, safe.ToString(CultureInfo.InvariantCulture), nameof(IndexText));
        OnPropertyChanged(nameof(IndexLabel));
    }

    [RelayCommand]
    private void AddTag()
    {
        if (!IsReadOnly)
        {
            _onAddTag?.Invoke(this);
        }
    }

    [RelayCommand]
    private void RemoveTag(SlotTagRowViewModel? tagRow)
    {
        if (!IsReadOnly && tagRow != null)
        {
            _onRemoveTag?.Invoke(this, tagRow);
        }
    }
}
