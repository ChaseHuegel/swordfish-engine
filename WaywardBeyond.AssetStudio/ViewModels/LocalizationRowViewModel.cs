using CommunityToolkit.Mvvm.ComponentModel;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     Represents one editable key-value translation entry inside a localization table file.
/// </summary>
public sealed partial class LocalizationRowViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action<LocalizationRowViewModel, bool, string, string>? _onValueTransition;
    private readonly Action<LocalizationRowViewModel, string, string>? _onKeyTransition;
    private string _key;
    private string _englishValue;
    private string _pseudoValue;

    [ObservableProperty]
    private bool _usePseudoLocale;

    [ObservableProperty]
    private bool _isDuplicateKey;

    /// <summary>
    ///     Initializes a localization table row with English and pseudo-locale text.
    /// </summary>
    public LocalizationRowViewModel(
        string key,
        string englishValue,
        string pseudoValue,
        bool usePseudoLocale,
        Action? onChanged = null,
        Action<LocalizationRowViewModel, bool, string, string>? onValueTransition = null,
        Action<LocalizationRowViewModel, string, string>? onKeyTransition = null)
    {
        _key = key ?? string.Empty;
        OriginalKey = _key;
        _englishValue = englishValue ?? string.Empty;
        _pseudoValue = pseudoValue ?? string.Empty;
        _usePseudoLocale = usePseudoLocale;
        _onChanged = onChanged;
        _onValueTransition = onValueTransition;
        _onKeyTransition = onKeyTransition;
    }

    /// <summary>
    ///     Last saved translation key persisted on disk, used to rename or remove old entries cleanly.
    /// </summary>
    public string OriginalKey { get; private set; }

    /// <summary>
    ///     Canonical dot-separated localization key.
    /// </summary>
    public string Key
    {
        get => _key;
        set
        {
            var safe = value ?? string.Empty;
            var oldKey = _key;
            if (!SetProperty(ref _key, safe))
            {
                return;
            }

            _onKeyTransition?.Invoke(this, oldKey, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     English translation string for this key.
    /// </summary>
    public string EnglishValue
    {
        get => _englishValue;
        set => SetProperty(ref _englishValue, value ?? string.Empty);
    }

    /// <summary>
    ///     Pseudo-locale preview string for this key.
    /// </summary>
    public string PseudoValue
    {
        get => _pseudoValue;
        set => SetProperty(ref _pseudoValue, value ?? string.Empty);
    }

    /// <summary>
    ///     Gets or sets the active localized value for the selected locale.
    /// </summary>
    public string ActiveValue
    {
        get => UsePseudoLocale ? PseudoValue : EnglishValue;
        set
        {
            var safe = value ?? string.Empty;
            var oldValue = ActiveValue;
            if (string.Equals(oldValue, safe, StringComparison.Ordinal))
            {
                return;
            }

            if (UsePseudoLocale)
            {
                PseudoValue = safe;
            }
            else
            {
                EnglishValue = safe;
            }

            OnPropertyChanged();
            _onValueTransition?.Invoke(this, UsePseudoLocale, oldValue, safe);
            _onChanged?.Invoke();
        }
    }

    /// <summary>
    ///     Updates <see cref="OriginalKey"/> to match <see cref="Key"/> after the document is saved to disk.
    /// </summary>
    public void CommitSavedKey()
    {
        OriginalKey = _key;
    }

    /// <summary>
    ///     Restores a translation key during undo or redo replay without recording a new edit.
    /// </summary>
    public void ApplyKeyForReplay(string restoredKey)
    {
        SetProperty(ref _key, restoredKey ?? string.Empty, nameof(Key));
    }

    /// <summary>
    ///     Restores a translation value during undo or redo replay without recording a new edit.
    /// </summary>
    public void ApplyValueForReplay(bool pseudoLocale, string restoredValue)
    {
        var safe = restoredValue ?? string.Empty;
        if (pseudoLocale)
        {
            PseudoValue = safe;
        }
        else
        {
            EnglishValue = safe;
        }

        OnPropertyChanged(nameof(ActiveValue));
    }

    partial void OnUsePseudoLocaleChanged(bool value)
    {
        OnPropertyChanged(nameof(ActiveValue));
    }
}
