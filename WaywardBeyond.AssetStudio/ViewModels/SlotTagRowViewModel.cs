using CommunityToolkit.Mvvm.ComponentModel;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable invariant tag reference inside a slot group or per-slot filter override.
/// </summary>
public sealed partial class SlotTagRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<SlotTagRowViewModel, string, string>? _onValueTransition;
    private string _tagName;
    private bool _isResolved;
    private bool _syncingPicker;

    /// <summary>
    ///     Initializes a slot tag row backed by a searchable invariant tag picker.
    /// </summary>
    public SlotTagRowViewModel(
        string tagName,
        Action? onChanged = null,
        Action<SlotTagRowViewModel, string, string>? onValueTransition = null,
        IReadOnlyList<ReferenceCandidate>? pickerCandidates = null,
        bool isReadOnly = false)
    {
        _tagName = tagName ?? string.Empty;
        _onChanged = onChanged;
        _onValueTransition = onValueTransition;
        TagPickerField = new StudioFieldViewModel(
            fieldKey: "AllowedTag",
            label: "Allowed Tag",
            grammarHint: "invariant tag · item.wearable",
            initialValue: _tagName,
            isReadOnly: isReadOnly,
            showRawTokenToggle: false,
            referenceKind: ReferenceKind.Tag,
            candidates: pickerCandidates,
            onValueChanged: HandlePickerValueChanged);
    }

    /// <summary>
    ///     Searchable invariant tag picker field backing this row.
    /// </summary>
    public StudioFieldViewModel TagPickerField { get; }

    /// <summary>
    ///     Invariant item tag identifier (for example <c>item.wearable.helmet</c>).
    /// </summary>
    public string TagName
    {
        get => _tagName;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _tagName;
            if (!SetProperty(ref _tagName, safe))
            {
                return;
            }

            if (!_syncingPicker)
            {
                _syncingPicker = true;
                try
                {
                    TagPickerField.ApplyValueForReplay(safe);
                }
                finally
                {
                    _syncingPicker = false;
                }
            }

            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(CanNavigate));
            _onValueTransition?.Invoke(this, oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Whether the referenced invariant tag resolves in the active workspace.
    /// </summary>
    public bool IsResolved
    {
        get => _isResolved;
        private set
        {
            if (SetProperty(ref _isResolved, value))
            {
                OnPropertyChanged(nameof(IsUnresolved));
            }
        }
    }

    /// <summary>
    ///     Whether the tag is non-empty and does not currently resolve in workspace invariant tags.
    /// </summary>
    public bool IsUnresolved => !string.IsNullOrWhiteSpace(TagName) && !IsResolved;

    /// <summary>
    ///     Whether this row carries a non-empty tag identifier for deep-link navigation.
    /// </summary>
    public bool CanNavigate => !string.IsNullOrWhiteSpace(TagName);

    /// <summary>
    ///     Formatted display label for preview chips.
    /// </summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(TagName) ? "(unset tag)" : TagName.Trim();

    /// <summary>
    ///     Refreshes tag resolution state against workspace reference candidates.
    /// </summary>
    public void RefreshResolution(IReadOnlyList<ReferenceCandidate> candidates)
    {
        var trimmed = TagName.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            IsResolved = false;
            TagPickerField.SetValidationState(false, null);
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Kind == ReferenceKind.Tag
                && string.Equals(candidate.Value, trimmed, StringComparison.Ordinal))
            {
                IsResolved = true;
                TagPickerField.SetValidationState(false, null);
                return;
            }
        }

        IsResolved = false;
        TagPickerField.SetValidationState(true, $"Tag '{trimmed}' was not found in invariant tags.");
    }

    /// <summary>
    ///     Updates the read-only state on the embedded tag picker field.
    /// </summary>
    public void SetReadOnly(bool isReadOnly)
    {
        TagPickerField.IsReadOnly = isReadOnly;
    }

    /// <summary>
    ///     Restores the tag value during undo or redo replay without recording another edit.
    /// </summary>
    public void ApplyValueForReplay(string restoredValue)
    {
        var safe = restoredValue ?? string.Empty;
        SetProperty(ref _tagName, safe, nameof(TagName));
        _syncingPicker = true;
        try
        {
            TagPickerField.ApplyValueForReplay(safe);
        }
        finally
        {
            _syncingPicker = false;
        }

        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(CanNavigate));
        OnPropertyChanged(nameof(IsUnresolved));
    }

    private void HandlePickerValueChanged(StudioFieldViewModel field)
    {
        if (_syncingPicker)
        {
            return;
        }

        _syncingPicker = true;
        try
        {
            TagName = field.Value;
        }
        finally
        {
            _syncingPicker = false;
        }
    }
}
