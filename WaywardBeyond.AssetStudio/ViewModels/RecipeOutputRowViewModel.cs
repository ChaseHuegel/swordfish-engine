using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WaywardBeyond.AssetStudio.Models;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable output item row on a recipe definition.
/// </summary>
public sealed partial class RecipeOutputRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<RecipeOutputRowViewModel, string, string, string>? _onPropertyTransition;
    private string _itemId;
    private int _count;
    private string _chance;
    private string? _resolvedThumbnailPath;
    private bool _isResolved;

    /// <summary>
    ///     Initializes a recipe output item row.
    /// </summary>
    public RecipeOutputRowViewModel(
        string itemId,
        int count,
        string chance,
        Action? onChanged = null,
        Action<RecipeOutputRowViewModel, string, string, string>? onPropertyTransition = null)
    {
        _itemId = itemId ?? string.Empty;
        _count = count > 0 ? count : 1;
        _chance = chance ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
    }

    /// <summary>
    ///     Produced item identifier.
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

            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(CanNavigate));
            _onPropertyTransition?.Invoke(this, nameof(ItemId), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Produced item count.
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
    ///     Optional drop probability in (0, 1]; blank represents guaranteed output (1.0).
    /// </summary>
    public string Chance
    {
        get => _chance;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _chance;
            if (!SetProperty(ref _chance, safe))
            {
                return;
            }

            OnPropertyChanged(nameof(HasChanceBadge));
            OnPropertyChanged(nameof(ChanceBadge));
            _onPropertyTransition?.Invoke(this, nameof(Chance), oldValue, safe);
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
    ///     Whether the referenced item resolves in the active workspace.
    /// </summary>
    public bool IsResolved
    {
        get => _isResolved;
        private set => SetProperty(ref _isResolved, value);
    }

    /// <summary>
    ///     Whether a thumbnail image is available for this output.
    /// </summary>
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ResolvedThumbnailPath);

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
    ///     Whether an explicit probability below 100% should be shown on preview chips.
    /// </summary>
    public bool HasChanceBadge => !string.IsNullOrWhiteSpace(ChanceBadge);

    /// <summary>
    ///     Formatted drop probability label (for example <c>25%</c>) when <see cref="Chance"/> is set below 1.
    /// </summary>
    public string ChanceBadge
    {
        get
        {
            var trimmed = Chance.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                return string.Empty;
            }

            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var probability)
                && double.IsFinite(probability))
            {
                if (probability >= 0.9999 && probability <= 1.0001)
                {
                    return string.Empty;
                }

                return string.Create(CultureInfo.InvariantCulture, $"{probability * 100.0:0.#}%");
            }

            return trimmed;
        }
    }

    /// <summary>
    ///     Refreshes item resolution state and icon thumbnail from workspace reference candidates.
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
            case nameof(Chance):
                SetProperty(ref _chance, restoredValue ?? string.Empty, nameof(Chance));
                OnPropertyChanged(nameof(HasChanceBadge));
                OnPropertyChanged(nameof(ChanceBadge));
                break;
        }
    }
}
