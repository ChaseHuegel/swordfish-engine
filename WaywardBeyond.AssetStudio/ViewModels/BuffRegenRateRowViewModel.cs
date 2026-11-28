using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable regeneration rate row on a buff definition.
/// </summary>
public sealed partial class BuffRegenRateRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<BuffRegenRateRowViewModel, string, string, string>? _onPropertyTransition;
    private string _statId;
    private string _perSecond;

    /// <summary>
    ///     Initializes a buff regeneration rate row.
    /// </summary>
    public BuffRegenRateRowViewModel(
        string statId,
        string perSecond,
        Action? onChanged = null,
        Action<BuffRegenRateRowViewModel, string, string, string>? onPropertyTransition = null,
        bool isReadOnly = false)
    {
        _statId = statId ?? string.Empty;
        _perSecond = perSecond ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
        IsReadOnly = isReadOnly;
    }

    /// <summary>
    ///     Whether this row belongs to a read-only document.
    /// </summary>
    public bool IsReadOnly { get; }

    /// <summary>
    ///     Target statistic identifier (for example <c>wb:health</c>).
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
    ///     Per-second delta text applied while the buff is active.
    /// </summary>
    public string PerSecond
    {
        get => _perSecond;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _perSecond;
            if (!SetProperty(ref _perSecond, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(PerSecond), oldValue, safe);
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
            case nameof(PerSecond):
                SetProperty(ref _perSecond, safe, nameof(PerSecond));
                break;
        }
    }
}
