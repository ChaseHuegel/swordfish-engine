using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WaywardBeyond.AssetStudio.Models;
using WaywardBeyond.AssetTools.Settings;

namespace WaywardBeyond.AssetStudio.ViewModels;

/// <summary>
///     View model backing the studio preferences dialog including theme, namespace, culture, GL, and layout reset.
/// </summary>
public sealed partial class SettingsDialogViewModel : ObservableObject
{
    private readonly Action<StudioSettingsState> _onSave;
    private readonly Action _onResetLayout;
    private readonly Action _onClose;
    private readonly double _uiScale;
    private string _defaultNamespace;

    [ObservableProperty]
    private ToolTheme _themePreference;

    [ObservableProperty]
    private string _cultureOverride;

    [ObservableProperty]
    private string _glPreference;

    [ObservableProperty]
    private string _logLevel;

    /// <summary>
    ///     Initializes the settings dialog from persisted user preferences.
    /// </summary>
    public SettingsDialogViewModel(
        StudioSettingsState initialState,
        Action<StudioSettingsState> onSave,
        Action onResetLayout,
        Action onClose)
    {
        _themePreference = initialState.Theme;
        _uiScale = ToolSettings.ClampUiScale(initialState.UiScale);
        _defaultNamespace = initialState.DefaultNamespace ?? string.Empty;
        _cultureOverride = initialState.CultureOverride ?? "System Default";
        _glPreference = initialState.GlPreference ?? "Auto";
        _logLevel = initialState.LogLevel ?? "Information";
        IsPortableMode = initialState.IsPortableMode;
        SettingsPath = initialState.SettingsPath;
        _onSave = onSave;
        _onResetLayout = onResetLayout;
        _onClose = onClose;
    }

    /// <summary>
    ///     Supported UI culture override options.
    /// </summary>
    public static IReadOnlyList<string> CULTURE_OPTIONS { get; } = ["System Default", "en (English)", "qps-ploc (Pseudo-locale)"];

    /// <summary>
    ///     Supported graphics renderer preferences.
    /// </summary>
    public static IReadOnlyList<string> GL_OPTIONS { get; } =
    [
        "Auto",
        "OpenGL Core (Desktop)",
        "OpenGL ES (ANGLE / EGL)",
    ];

    /// <summary>
    ///     Supported diagnostic log verbosity levels.
    /// </summary>
    public static IReadOnlyList<string> LOG_LEVEL_OPTIONS { get; } = ["Warning", "Information", "Debug", "Trace"];

    /// <summary>
    ///     Whether a portable marker file keeps settings beside the executable.
    /// </summary>
    public bool IsPortableMode { get; }

    /// <summary>
    ///     Display label describing portable vs per-user storage mode.
    /// </summary>
    public string PortableModeLabel => IsPortableMode
        ? "Portable mode active (settings stored beside the app)"
        : "Place a 'portable' file next to the app to store settings locally.";

    /// <summary>
    ///     Effective path where settings are stored.
    /// </summary>
    public string SettingsPath { get; }

    /// <summary>
    ///     Default namespace prefix pre-filled for new assets in the active workspace.
    /// </summary>
    public string DefaultNamespace
    {
        get => _defaultNamespace;
        set => SetProperty(ref _defaultNamespace, value ?? string.Empty);
    }

    /// <summary>
    ///     Whether the System theme option is selected.
    /// </summary>
    public bool IsSystemTheme => ThemePreference == ToolTheme.System;

    /// <summary>
    ///     Whether the Light theme option is selected.
    /// </summary>
    public bool IsLightTheme => ThemePreference == ToolTheme.Light;

    /// <summary>
    ///     Whether the Dark theme option is selected.
    /// </summary>
    public bool IsDarkTheme => ThemePreference == ToolTheme.Dark;

    partial void OnThemePreferenceChanged(ToolTheme value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    [RelayCommand]
    private void SelectSystemTheme() => ThemePreference = ToolTheme.System;

    [RelayCommand]
    private void SelectLightTheme() => ThemePreference = ToolTheme.Light;

    [RelayCommand]
    private void SelectDarkTheme() => ThemePreference = ToolTheme.Dark;

    [RelayCommand]
    private void ResetPanelLayout()
    {
        _onResetLayout();
    }

    [RelayCommand]
    private void Save()
    {
        _onSave(new StudioSettingsState(
            IsDarkTheme: ThemePreference == ToolTheme.Dark,
            CultureOverride: CultureOverride,
            GlPreference: GlPreference,
            LogLevel: LogLevel,
            IsPortableMode: IsPortableMode,
            SettingsPath: SettingsPath,
            Theme: ThemePreference,
            UiScale: _uiScale,
            DefaultNamespace: DefaultNamespace.Trim().TrimEnd(':').Trim()));
    }

    [RelayCommand]
    private void Cancel()
    {
        _onClose();
    }
}
