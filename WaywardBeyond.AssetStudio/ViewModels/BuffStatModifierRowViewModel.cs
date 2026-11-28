using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable statistic modifier row on a buff definition.
/// </summary>
public sealed partial class BuffStatModifierRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<BuffStatModifierRowViewModel, string, string, string>? _onPropertyTransition;
    private string _statId;
    private string _flatDelta;
    private string _percentDelta;

    /// <summary>
    ///     Initializes a buff statistic modifier row.
    /// </summary>
    public BuffStatModifierRowViewModel(
        string statId,
        string flatDelta,
        string percentDelta,
        Action? onChanged = null,
        Action<BuffStatModifierRowViewModel, string, string, string>? onPropertyTransition = null,
        bool isReadOnly = false)
    {
        _statId = statId ?? string.Empty;
        _flatDelta = flatDelta ?? string.Empty;
        _percentDelta = percentDelta ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
        IsReadOnly = isReadOnly;
    }

    /// <summary>
    ///     Whether this row belongs to a read-only document.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    ///     Target statistic identifier (for example <c>wb:move_speed</c>).
    /// </summary>
    public string StatId
    {
        get => _statId;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _statId;
            if (!SetProperty(ref _statId, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(StatId), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Optional flat additive delta text.
    /// </summary>
    public string FlatDelta
    {
        get => _flatDelta;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _flatDelta;
            if (!SetProperty(ref _flatDelta, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(FlatDelta), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Optional multiplicative percent delta text (for example <c>0.25</c> for +25%).
    /// </summary>
    public string PercentDelta
    {
        get => _percentDelta;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _percentDelta;
            if (!SetProperty(ref _percentDelta, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(PercentDelta), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Restores a named property during undo or redo replay without pushing another edit.
    /// </summary>
    public void ApplyPropertyForReplay(string propertyName, string restoredValue)
    {
        var safe = restoredValue ?? string.Empty;
        switch (propertyName)
        {
            case nameof(StatId):
                SetProperty(ref _statId, safe, nameof(StatId));
                break;
            case nameof(FlatDelta):
                SetProperty(ref _flatDelta, safe, nameof(FlatDelta));
                break;
            case nameof(PercentDelta):
                SetProperty(ref _percentDelta, safe, nameof(PercentDelta));
                break;
        }
    }
}
