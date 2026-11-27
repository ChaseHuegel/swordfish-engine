using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one custom numeric statistic key-value row on an item definition.
/// </summary>
public sealed partial class ItemStatisticRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<ItemStatisticRowViewModel, string, string, string>? _onPropertyTransition;
    private string _key;
    private string _value;

    [ObservableProperty]
    private bool _isDuplicateKey;

    /// <summary>
    ///     Initializes an item statistic row with its key and numeric value text.
    /// </summary>
    public ItemStatisticRowViewModel(
        string key,
        string value,
        Action? onChanged = null,
        Action<ItemStatisticRowViewModel, string, string, string>? onPropertyTransition = null)
    {
        _key = key ?? string.Empty;
        _value = value ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
    }

    /// <summary>
    ///     Statistic identifier (for example <c>wb:mining_speed</c> or a custom mod statistic key).
    /// </summary>
    public string Key
    {
        get => _key;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _key;
            if (!SetProperty(ref _key, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(Key), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Numeric value text for this statistic.
    /// </summary>
    public string Value
    {
        get => _value;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = _value;
            if (!SetProperty(ref _value, safe))
            {
                return;
            }

            _onPropertyTransition?.Invoke(this, nameof(Value), oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Restores a named property during undo or redo replay without pushing another edit.
    /// </summary>
    public void ApplyPropertyForReplay(string propertyName, string restoredValue)
    {
        switch (propertyName)
        {
            case nameof(Key):
                SetProperty(ref _key, restoredValue ?? string.Empty, nameof(Key));
                break;
            case nameof(Value):
                SetProperty(ref _value, restoredValue ?? string.Empty, nameof(Value));
                break;
        }
    }
}
