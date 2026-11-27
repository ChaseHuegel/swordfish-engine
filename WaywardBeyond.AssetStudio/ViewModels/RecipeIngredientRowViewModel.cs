using CommunityToolkit.Mvvm.ComponentModel;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable input ingredient row on a recipe definition.
/// </summary>
public sealed partial class RecipeIngredientRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<RecipeIngredientRowViewModel, string, string, string>? _onPropertyTransition;
    private string _itemId;
    private int _count;
    private string _slotGroup;
    private string? _resolvedThumbnailPath;
    private bool _isResolved;

    /// <summary>
    ///     Initializes a recipe input ingredient row.
    /// </summary>
    public RecipeIngredientRowViewModel(
        string itemId,
        int count,
        string slotGroup,
        Action? onChanged = null,
        Action<RecipeIngredientRowViewModel, string, string, string>? onPropertyTransition = null)
    {
        _itemId = itemId ?? string.Empty;
        _count = count > 0 ? count : 1;
        _slotGroup = slotGroup ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
    }

    /// <summary>
    ///     Item identifier or <c>tag:&lt;name&gt;</c> selector.
    /// </summary>
    public string ItemId
    {
        get => _itemId;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _itemId;
            if (!SetProperty(ref _itemId, safe))
            {
                return;
            }

            OnPropertyChanged(nameof(IsTagSelector));
            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(CanNavigate));
            _onPropertyTransition?.Invoke(this, nameof(ItemId), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Required item count.
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            var safe = value < 1 ? 1 : value;
            var oldValue = _count;
            if (!SetProperty(ref _count, safe))
            {
                return;
            }

            OnPropertyChanged(nameof(CountLabel));
            _onPropertyTransition?.Invoke(this, nameof(Count), oldValue.ToString(), safe.ToString());
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Optional target slot group identifier.
    /// </summary>
    public string SlotGroup
    {
        get => _slotGroup;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _slotGroup;
            if (!SetProperty(ref _slotGroup, safe))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSlotGroup));
            _onPropertyTransition?.Invoke(this, nameof(SlotGroup), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Optional absolute path to the resolved item icon thumbnail for preview rendering.
    /// </summary>
    public string? ResolvedThumbnailPath
    {
        get => _resolvedThumbnailPath;
        private set
        {
            if (SetProperty(ref _resolvedThumbnailPath, value))
            {
                OnPropertyChanged(nameof(HasThumbnail));
            }
        }
    }

    /// <summary>
    ///     Whether the referenced item or invariant tag resolves in the active workspace.
    /// </summary>
    public bool IsResolved
    {
        get => _isResolved;
        private set => SetProperty(ref _isResolved, value);
    }

    /// <summary>
    ///     Whether a thumbnail image is available for this ingredient.
    /// </summary>
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ResolvedThumbnailPath);

    /// <summary>
    ///     Whether this input uses a <c>tag:&lt;name&gt;</c> selector instead of a concrete item ID.
    /// </summary>
    public bool IsTagSelector => ItemId.Trim().StartsWith("tag:", StringComparison.Ordinal);

    /// <summary>
    ///     Whether a non-empty slot group restriction is configured.
    /// </summary>
    public bool HasSlotGroup => !string.IsNullOrWhiteSpace(SlotGroup);

    /// <summary>
    ///     Whether this row carries a non-empty target identifier for deep-link navigation.
    /// </summary>
    public bool CanNavigate => !string.IsNullOrWhiteSpace(ItemId);

    /// <summary>
    ///     Formatted display label for preview chips.
    /// </summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(ItemId) ? "(unset item)" : ItemId.Trim();

    /// <summary>
    ///     Formatted quantity badge for preview chips.
    /// </summary>
    public string CountLabel => $"×{Count}";

    /// <summary>
    ///     Refreshes item/tag resolution state and icon thumbnail from workspace reference candidates.
    /// </summary>
    public void RefreshResolution(IReadOnlyList<ReferenceCandidate> candidates)
    {
        var trimmed = ItemId.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            IsResolved = false;
            ResolvedThumbnailPath = null;
            return;
        }

        if (trimmed.StartsWith("tag:", StringComparison.Ordinal))
        {
            var tagName = trimmed[4..];
            var foundTag = false;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind == ReferenceKind.Tag
                    && string.Equals(candidates[i].Value, tagName, StringComparison.Ordinal))
                {
                    foundTag = true;
                    break;
                }
            }

            IsResolved = foundTag;
            ResolvedThumbnailPath = null;
            return;
        }

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Kind == ReferenceKind.ItemId
                && string.Equals(candidate.Value, trimmed, StringComparison.Ordinal))
            {
                IsResolved = true;
                ResolvedThumbnailPath = candidate.ThumbnailPath;
                return;
            }
        }

        IsResolved = false;
        ResolvedThumbnailPath = null;
    }

    /// <summary>
    ///     Restores a named property during undo or redo replay without pushing another edit.
    /// </summary>
    public void ApplyPropertyForReplay(string propertyName, string restoredValue)
    {
        switch (propertyName)
        {
            case nameof(ItemId):
                SetProperty(ref _itemId, restoredValue ?? string.Empty, nameof(ItemId));
                OnPropertyChanged(nameof(IsTagSelector));
                OnPropertyChanged(nameof(DisplayLabel));
                OnPropertyChanged(nameof(CanNavigate));
                break;
            case nameof(Count):
                if (int.TryParse(restoredValue, out var parsedCount))
                {
                    SetProperty(ref _count, Math.Max(1, parsedCount), nameof(Count));
                    OnPropertyChanged(nameof(CountLabel));
                }

                break;
            case nameof(SlotGroup):
                SetProperty(ref _slotGroup, restoredValue ?? string.Empty, nameof(SlotGroup));
                OnPropertyChanged(nameof(HasSlotGroup));
                break;
        }
    }
}
