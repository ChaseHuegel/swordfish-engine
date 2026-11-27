using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one custom numeric statistic key-value row on an item or brick definition.
/// </summary>
public sealed partial class StatisticRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<StatisticRowViewModel, string, string, string>? _onPropertyTransition;
    private string _key;
    private string _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyError))]
    private bool _isDuplicateKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasKeyError))]
    private bool _isInvalidKey;

    [ObservableProperty]
    private bool _isInvalidValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string? _validationError;

    /// <summary>
    ///     Initializes a statistic row with its key and numeric value text.
    /// </summary>
    public StatisticRowViewModel(
        string key,
        string value,
        Action? onChanged = null,
        Action<StatisticRowViewModel, string, string, string>? onPropertyTransition = null)
    {
        _key = key ?? string.Empty;
        _value = value ?? string.Empty;
        _onChanged = onChanged;
        _onPropertyTransition = onPropertyTransition;
    }

    /// <summary>
    ///     Whether the key input currently has a validation error (empty or duplicate).
    /// </summary>
    public bool HasKeyError => IsDuplicateKey || IsInvalidKey;

    /// <summary>
    ///     Whether this row currently has an inline validation message to display.
    /// </summary>
    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    /// <summary>
    ///     Statistic identifier (for example <c>wb:hardness</c>, <c>wb:mining_speed</c>, or a custom mod statistic key).
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
