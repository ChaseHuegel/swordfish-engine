using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Settings;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus;

internal abstract class SettingsPage<TIdentifier>(
    in ConfigManager configManager,
    in ControlSettings controlSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in GameplayConfig gameplayConfig,
    in UISettings uiSettings,
    in SoundEffectService soundEffectService,
    in IWindowContext windowContext,
    in IInputService inputService,
    in ILocalization localization
) : IMenuPage<TIdentifier> where TIdentifier : notnull
{
    private readonly SettingsDisplayMode[] _displayModes =
    [
        SettingsDisplayMode.Windowed,
        SettingsDisplayMode.BorderlessWindow,
        SettingsDisplayMode.Maximized,
        SettingsDisplayMode.BorderlessMaximized,
        SettingsDisplayMode.Fullscreen,
        SettingsDisplayMode.BorderlessFullscreen,
    ];

    private readonly AntiAliasing[] _antiAliasingOptions =
    [
        AntiAliasing.None,
        AntiAliasing.MSAA,
    ];

    private readonly CornerCapStyle _panelCaps = new()
    {
        Length = SettingsMetrics.PanelCapLength,
        Thickness = SettingsMetrics.PanelCapThickness,
        Color = SettingsTheme.Border,
    };

    private readonly ConfigManager _configManager = configManager;
    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly WindowSettings _windowSettings = windowSettings;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly GameplayConfig _gameplayConfig = gameplayConfig;
    private readonly UISettings _uiSettings = uiSettings;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly IWindowContext _windowContext = windowContext;
    private readonly IInputService _inputService = inputService;
    private readonly ILocalization _localization = localization;

    private SettingsRenderer? _renderer;
    private SettingsDraft? _draft;
    private SettingsCategory[]? _categories;
    private int _categoryIndex;
    private int _scrollY;
    private string? _status;
    private double _statusUntil;

    public abstract TIdentifier ID { get; }

    /// <inheritdoc/>
    public void OnEnter()
    {
        _draft = SettingsDraft.FromConfigs(
            _controlSettings,
            _windowSettings,
            _renderSettings,
            _volumeSettings,
            _gameplayConfig,
            _uiSettings,
            _windowContext
        );
        _categories = BuildCategories();
        _categoryIndex = 0;
        _scrollY = 0;
        _status = null;
        _statusUntil = 0d;
    }

    /// <inheritdoc/>
    public void OnExit()
    {
        _draft = null;
        _categories = null;
        _status = null;
    }

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<TIdentifier> menu)
    {
        if (_draft == null || _categories == null)
        {
            OnEnter();
        }

        if (_draft == null || _categories == null)
        {
            return Result.FromFailure("Failed to prepare the page.");
        }

        RefreshExternalDisplaySettings();

        SettingsLayout layout = SettingsLayout.Compute(ui.Width, ui.Height);
        _renderer ??= new SettingsRenderer(ui, _localization, _soundEffectService, _inputService);
        _renderer.SetLayout(layout);
        SettingsRenderer renderer = _renderer;

        using (ui.Element("SettingsPage"))
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };
            ui.ClipConstraints = new Constraints
            {
                Width = new Fixed(layout.PageWidth),
                Height = new Fixed(layout.PageHeight),
            };

            renderer.DrawPageTitle();

            if (layout.UseTabs)
            {
                if (renderer.DrawTabs(_categories, ref _categoryIndex))
                {
                    _scrollY = 0;
                }
            }
            else
            {
                using (ui.SettingsPanel(layout.Tray, SettingsTheme.PanelBackground, border: default, _panelCaps))
                {
                    if (renderer.DrawTray(_categories, ref _categoryIndex))
                    {
                        _scrollY = 0;
                    }
                }
            }

            using (ui.SettingsPanel(layout.Pane, SettingsTheme.PanelBackground, border: default, _panelCaps))
            {
                renderer.DrawPane(_categories[_categoryIndex], ref _scrollY);
            }

            bool dirty = IsDirty();
            string? status = _status != null && ui.Time < _statusUntil ? _status : null;
            renderer.DrawFooter(dirty, status, out bool cancelClicked, out bool applyClicked);

            if (cancelClicked)
            {
                PlayButton();
                menu.GoBack();
            }
            else if (applyClicked)
            {
                ApplySettings(ui.Time);
            }
        }

        return Result.FromSuccess();
    }

    private void ApplySettings(double time)
    {
        SettingsDraft draft = _draft ?? throw new InvalidOperationException("The settings draft is not initialized.");

        bool displayChanged = IsEntryDirty("Settings.DisplayMode") || IsEntryDirty("Settings.Resolution");
        if (displayChanged)
        {
            _windowContext.ApplyDisplaySettings(
                draft.DisplayMode.ToWindowMode(),
                draft.DisplayMode.IsBorderless(),
                draft.Resolution.Width,
                draft.Resolution.Height
            );
        }

        _renderSettings.VSync.Set(draft.VSync);
        _renderSettings.Framerate.Set(draft.FrameLimit);
        _renderSettings.FOV.Set(draft.Fov);
        _renderSettings.FarPlane.Set(draft.ViewDistance);
        _renderSettings.AntiAliasing.Set(draft.AntiAliasingMode);
        _renderSettings.SAO.Enabled.Set(draft.AmbientOcclusion);
        _renderSettings.SAO.Samples.Set(draft.AoSamples);
        _renderSettings.Bloom.Enabled.Set(draft.Bloom);
        _renderSettings.Bloom.Passes.Set(draft.BloomPasses);

        _volumeSettings.Master.Set(draft.MasterVolume);
        _volumeSettings.Music.Set(draft.MusicVolume);
        _volumeSettings.Effects.Set(draft.EffectsVolume);
        _volumeSettings.Interface.Set(draft.InterfaceVolume);

        _gameplayConfig.Autosave.Set(draft.Autosave);
        _gameplayConfig.AutosaveIntervalMs.Set(draft.AutosaveIntervalMinutes * 60000);
        _gameplayConfig.ControlHints.Set(draft.ControlHints);
        _gameplayConfig.Crosshair.Set(draft.Crosshair);

        _uiSettings.NameplateDistance.Set(draft.NameTagDistance);

        _controlSettings.LookSensitivity.Set(draft.MouseSensitivity);
        _controlSettings.RememberShape.Set(draft.RememberShape);
        _controlSettings.RememberOrientation.Set(draft.RememberOrientation);

        _configManager.ApplySettings();

        if (_categories != null)
        {
            for (var i = 0; i < _categories.Length; i++)
            {
                _categories[i].CaptureBaselines();
            }
        }

        _status = L("ui.settings.saved");
        _statusUntil = time + 2d;
        PlayButton();
    }

    private SettingsCategory[] BuildCategories()
    {
        return
        [
            BuildDisplayCategory(),
            BuildAudioCategory(),
            BuildGameplayCategory(),
            BuildControlsCategory(),
        ];
    }

    private SettingsCategory BuildDisplayCategory()
    {
        return new SettingsCategory(
            id: "display",
            titleKey: "ui.settings.category.display",
            rows:
            [
                new SettingsSection("ui.settings.section.window"),
                new StepperEntry<SettingsDisplayMode>(
                    id: "Settings.DisplayMode",
                    labelKey: "ui.settings.displayMode",
                    get: () => _draft!.DisplayMode,
                    set: value => _draft!.DisplayMode = value,
                    defaultValue: () => SettingsDefaults.DISPLAY_MODE,
                    options: _displayModes,
                    format: FormatDisplayMode
                ),
                new StepperEntry<SettingsResolution>(
                    id: "Settings.Resolution",
                    labelKey: "ui.settings.resolution",
                    get: () => _draft!.Resolution,
                    set: value => _draft!.Resolution = value,
                    defaultValue: () => SettingsDraft.GetNativeResolution(_windowContext),
                    options: GetResolutionOptions(),
                    format: resolution => $"{resolution.Width} x {resolution.Height}",
                    isEnabled: () => _draft!.DisplayMode.UsesWindowSize()
                ),
                new ToggleEntry(
                    id: "Settings.VSync",
                    labelKey: "ui.settings.vsync",
                    get: () => _draft!.VSync,
                    set: value => _draft!.VSync = value,
                    defaultValue: () => SettingsDefaults.V_SYNC
                ),
                new StepperEntry<int>(
                    id: "Settings.FrameLimit",
                    labelKey: "ui.settings.frameLimit",
                    get: () => _draft!.FrameLimit,
                    set: value => _draft!.FrameLimit = value,
                    defaultValue: () => SettingsDefaults.FRAME_LIMIT,
                    options: SettingsDefaults.GetFrameLimits(_draft?.FrameLimit ?? SettingsDefaults.FRAME_LIMIT),
                    format: FormatFrameLimit,
                    isEnabled: () => !_draft!.VSync
                ),
                new SettingsSection("ui.settings.section.view"),
                new SliderEntry(
                    id: "Settings.Fov",
                    labelKey: "ui.settings.fov",
                    get: () => _draft!.Fov,
                    set: value => _draft!.Fov = (int)MathF.Round(value),
                    defaultValue: () => SettingsDefaults.FOV,
                    min: SettingsDefaults.FOV_MIN,
                    max: SettingsDefaults.FOV_MAX,
                    step: 1f,
                    format: FormatDegrees
                ),
                new StepperEntry<float>(
                    id: "Settings.ViewDistance",
                    labelKey: "ui.settings.viewDistance",
                    get: () => _draft!.ViewDistance,
                    set: value => _draft!.ViewDistance = value,
                    defaultValue: () => SettingsDefaults.VIEW_DISTANCE,
                    options: SettingsDefaults.GetViewDistances(_draft?.ViewDistance ?? SettingsDefaults.VIEW_DISTANCE),
                    format: FormatDistance
                ),
                new SettingsSection("ui.settings.section.quality"),
                new ChoiceEntry<AntiAliasing>(
                    id: "Settings.AntiAliasing",
                    labelKey: "ui.settings.antiAliasing",
                    get: () => _draft!.AntiAliasingMode,
                    set: value => _draft!.AntiAliasingMode = value,
                    defaultValue: () => SettingsDefaults.ANTI_ALIASING_MODE,
                    options: _antiAliasingOptions,
                    optionLabel: FormatAntiAliasing
                ),
                new ToggleEntry(
                    id: "Settings.AmbientOcclusion",
                    labelKey: "ui.settings.ambientOcclusion",
                    get: () => _draft!.AmbientOcclusion,
                    set: value => _draft!.AmbientOcclusion = value,
                    defaultValue: () => SettingsDefaults.AMBIENT_OCCLUSION
                ),
                new StepperEntry<int>(
                    id: "Settings.AoSamples",
                    labelKey: "ui.settings.aoSamples",
                    get: () => _draft!.AoSamples,
                    set: value => _draft!.AoSamples = value,
                    defaultValue: () => SettingsDefaults.AO_SAMPLES,
                    options: SettingsDefaults.GetAoSamples(_draft?.AoSamples ?? SettingsDefaults.AO_SAMPLES),
                    format: FormatSamples,
                    isEnabled: () => _draft!.AmbientOcclusion
                ),
                new ToggleEntry(
                    id: "Settings.Bloom",
                    labelKey: "ui.settings.bloom",
                    get: () => _draft!.Bloom,
                    set: value => _draft!.Bloom = value,
                    defaultValue: () => SettingsDefaults.BLOOM
                ),
                new StepperEntry<int>(
                    id: "Settings.BloomPasses",
                    labelKey: "ui.settings.bloomPasses",
                    get: () => _draft!.BloomPasses,
                    set: value => _draft!.BloomPasses = value,
                    defaultValue: () => SettingsDefaults.BLOOM_PASSES,
                    options: SettingsDefaults.GetBloomPasses(_draft?.BloomPasses ?? SettingsDefaults.BLOOM_PASSES),
                    format: FormatInteger,
                    isEnabled: () => _draft!.Bloom
                ),
                new SettingsResetRow("Settings.Reset.display", () => ResetCategory("display")),
            ]
        );
    }

    private SettingsCategory BuildAudioCategory()
    {
        return new SettingsCategory(
            id: "audio",
            titleKey: "ui.settings.category.audio",
            rows:
            [
                new SettingsSection("ui.settings.section.mix"),
                BuildVolumeSlider(
                    "Settings.MasterVolume",
                    "ui.settings.masterVolume",
                    () => _draft!.MasterVolume,
                    value => _draft!.MasterVolume = value,
                    SettingsDefaults.MASTER_VOLUME
                ),
                BuildVolumeSlider(
                    "Settings.MusicVolume",
                    "ui.settings.musicVolume",
                    () => _draft!.MusicVolume,
                    value => _draft!.MusicVolume = value,
                    SettingsDefaults.MUSIC_VOLUME
                ),
                BuildVolumeSlider(
                    "Settings.EffectsVolume",
                    "ui.settings.effectsVolume",
                    () => _draft!.EffectsVolume,
                    value => _draft!.EffectsVolume = value,
                    SettingsDefaults.EFFECTS_VOLUME
                ),
                BuildVolumeSlider(
                    "Settings.InterfaceVolume",
                    "ui.settings.interfaceVolume",
                    () => _draft!.InterfaceVolume,
                    value => _draft!.InterfaceVolume = value,
                    SettingsDefaults.INTERFACE_VOLUME
                ),
                new SettingsResetRow("Settings.Reset.audio", () => ResetCategory("audio")),
            ]
        );
    }

    private SettingsCategory BuildGameplayCategory()
    {
        return new SettingsCategory(
            id: "gameplay",
            titleKey: "ui.settings.category.gameplay",
            rows:
            [
                new SettingsSection("ui.settings.section.autosave"),
                new ToggleEntry(
                    id: "Settings.Autosave",
                    labelKey: "ui.settings.autosave",
                    get: () => _draft!.Autosave,
                    set: value => _draft!.Autosave = value,
                    defaultValue: () => SettingsDefaults.AUTOSAVE
                ),
                new StepperEntry<int>(
                    id: "Settings.AutosaveInterval",
                    labelKey: "ui.settings.autosaveInterval",
                    get: () => _draft!.AutosaveIntervalMinutes,
                    set: value => _draft!.AutosaveIntervalMinutes = value,
                    defaultValue: () => SettingsDefaults.AUTOSAVE_INTERVAL_MINUTES,
                    options: SettingsDefaults.GetAutosaveIntervals(_draft?.AutosaveIntervalMinutes ?? SettingsDefaults.AUTOSAVE_INTERVAL_MINUTES),
                    format: FormatMinutes,
                    isEnabled: () => _draft!.Autosave
                ),
                new SettingsSection("ui.settings.section.hud"),
                new ToggleEntry(
                    id: "Settings.ControlHints",
                    labelKey: "ui.settings.controlHints",
                    get: () => _draft!.ControlHints,
                    set: value => _draft!.ControlHints = value,
                    defaultValue: () => SettingsDefaults.CONTROL_HINTS
                ),
                new ToggleEntry(
                    id: "Settings.Crosshair",
                    labelKey: "ui.settings.crosshair",
                    get: () => _draft!.Crosshair,
                    set: value => _draft!.Crosshair = value,
                    defaultValue: () => SettingsDefaults.CROSSHAIR
                ),
                new SliderEntry(
                    id: "Settings.NameTagDistance",
                    labelKey: "ui.settings.nameTagDistance",
                    get: () => _draft!.NameTagDistance,
                    set: value => _draft!.NameTagDistance = (int)MathF.Round(value),
                    defaultValue: () => SettingsDefaults.NAME_TAG_DISTANCE,
                    min: SettingsDefaults.NAME_TAG_MIN,
                    max: SettingsDefaults.NAME_TAG_MAX,
                    step: SettingsDefaults.NAME_TAG_STEP,
                    format: FormatNameTagDistance
                ),
                new SettingsResetRow("Settings.Reset.gameplay", () => ResetCategory("gameplay")),
            ]
        );
    }

    private SettingsCategory BuildControlsCategory()
    {
        return new SettingsCategory(
            id: "controls",
            titleKey: "ui.settings.category.controls",
            rows:
            [
                new SettingsSection("ui.settings.section.mouse"),
                new SliderEntry(
                    id: "Settings.MouseSensitivity",
                    labelKey: "ui.settings.mouseSensitivity",
                    get: () => _draft!.MouseSensitivity,
                    set: value => _draft!.MouseSensitivity = (int)MathF.Round(value),
                    defaultValue: () => SettingsDefaults.MOUSE_SENSITIVITY,
                    min: SettingsDefaults.SENSITIVITY_MIN,
                    max: SettingsDefaults.SENSITIVITY_MAX,
                    step: 1f,
                    format: FormatInteger
                ),
                new SettingsSection("ui.settings.section.placement"),
                new ToggleEntry(
                    id: "Settings.RememberShape",
                    labelKey: "ui.settings.rememberShape",
                    get: () => _draft!.RememberShape,
                    set: value => _draft!.RememberShape = value,
                    defaultValue: () => SettingsDefaults.REMEMBER_SHAPE
                ),
                new ToggleEntry(
                    id: "Settings.RememberOrientation",
                    labelKey: "ui.settings.rememberOrientation",
                    get: () => _draft!.RememberOrientation,
                    set: value => _draft!.RememberOrientation = value,
                    defaultValue: () => SettingsDefaults.REMEMBER_ORIENTATION
                ),
                new SettingsResetRow("Settings.Reset.controls", () => ResetCategory("controls")),
            ]
        );
    }

    private SliderEntry BuildVolumeSlider(
        string id,
        string labelKey,
        Func<float> get,
        Action<float> set,
        float defaultVolume
    )
    {
        return new SliderEntry(
            id: id,
            labelKey: labelKey,
            get: get,
            set: set,
            defaultValue: () => defaultVolume,
            min: 0f,
            max: 1f,
            step: 0.05f,
            format: FormatPercent
        );
    }

    private IReadOnlyList<SettingsResolution> GetResolutionOptions()
    {
        var options = new List<SettingsResolution>();
        var seen = new HashSet<(int Width, int Height)>();

        void Add(SettingsResolution resolution, bool alwaysInclude = false)
        {
            if (resolution.Width <= 0 || resolution.Height <= 0)
            {
                return;
            }

            if (!alwaysInclude && (resolution.Width < 800 || resolution.Height < 600))
            {
                return;
            }

            if (seen.Add((resolution.Width, resolution.Height)))
            {
                options.Add(resolution);
            }
        }

        if (_draft != null)
        {
            Add(_draft.Resolution, alwaysInclude: true);
        }

        IReadOnlyList<Vector2> videoModes = _windowContext.GetVideoModes();
        for (var i = 0; i < videoModes.Count; i++)
        {
            Vector2 mode = videoModes[i];
            Add(new SettingsResolution((int)mode.X, (int)mode.Y));
        }

        Add(SettingsDraft.GetNativeResolution(_windowContext), alwaysInclude: true);

        options.Sort(static (left, right) =>
        {
            int widthComparison = left.Width.CompareTo(right.Width);
            return widthComparison != 0 ? widthComparison : left.Height.CompareTo(right.Height);
        });

        return options;
    }

    private void RefreshExternalDisplaySettings()
    {
        if (_draft == null)
        {
            return;
        }

        SettingsEntry? displayModeEntry = FindEntry("Settings.DisplayMode");
        SettingsDisplayMode currentMode = SettingsDisplayModeExtensions.FromWindowSettings(
            _windowSettings.Mode.Get(),
            _windowSettings.Borderless.Get()
        );

        if (displayModeEntry != null && !displayModeEntry.IsDirty && _draft.DisplayMode != currentMode)
        {
            _draft.DisplayMode = currentMode;
            displayModeEntry.CaptureBaseline();
        }

        SettingsEntry? resolutionEntry = FindEntry("Settings.Resolution");
        SettingsResolution currentResolution = SettingsDraft.GetWindowResolution(_windowSettings, _windowContext);

        if (resolutionEntry != null && !resolutionEntry.IsDirty && _draft.Resolution != currentResolution)
        {
            _draft.Resolution = currentResolution;
            resolutionEntry.CaptureBaseline();
        }
    }

    private SettingsEntry? FindEntry(string id)
    {
        if (_categories == null)
        {
            return null;
        }

        for (var i = 0; i < _categories.Length; i++)
        {
            SettingsEntry[] entries = _categories[i].Entries;
            for (var n = 0; n < entries.Length; n++)
            {
                if (entries[n].Id == id)
                {
                    return entries[n];
                }
            }
        }

        return null;
    }

    private bool IsEntryDirty(string id)
    {
        SettingsEntry? entry = FindEntry(id);
        return entry != null && entry.IsDirty;
    }

    private bool IsDirty()
    {
        if (_categories == null)
        {
            return false;
        }

        for (var i = 0; i < _categories.Length; i++)
        {
            if (_categories[i].IsDirty)
            {
                return true;
            }
        }

        return false;
    }

    private void ResetCategory(string categoryId)
    {
        if (_categories == null)
        {
            return;
        }

        for (var i = 0; i < _categories.Length; i++)
        {
            if (_categories[i].Id == categoryId)
            {
                _categories[i].ResetToDefaults();
                return;
            }
        }
    }

    private void PlayButton()
    {
        Widgets.Interactions.Click.WithButtonAudio(_soundEffectService);
    }

    private string L(string key)
    {
        return _localization.GetString(key) ?? key;
    }

    private string FormatDisplayMode(SettingsDisplayMode mode)
    {
        return mode switch
        {
            SettingsDisplayMode.Windowed => L("ui.settings.displayMode.windowed"),
            SettingsDisplayMode.BorderlessWindow => L("ui.settings.displayMode.borderlessWindow"),
            SettingsDisplayMode.Maximized => L("ui.settings.displayMode.maximized"),
            SettingsDisplayMode.BorderlessMaximized => L("ui.settings.displayMode.borderlessMaximized"),
            SettingsDisplayMode.Fullscreen => L("ui.settings.displayMode.fullscreen"),
            SettingsDisplayMode.BorderlessFullscreen => L("ui.settings.displayMode.borderlessFullscreen"),
            _ => mode.ToString(),
        };
    }

    private string FormatAntiAliasing(AntiAliasing antiAliasing)
    {
        return antiAliasing == AntiAliasing.MSAA
            ? L("ui.settings.antiAliasing.msaa")
            : L("ui.settings.antiAliasing.off");
    }

    private string FormatFrameLimit(int value)
    {
        return value == 0
            ? L("ui.settings.unlimited")
            : $"{value} {L("ui.unit.fps")}";
    }

    private string FormatDistance(float value)
    {
        return value >= 1000f
            ? $"{value / 1000f:0.#} {L("ui.unit.kilometers")}"
            : $"{MathF.Round(value)} {L("ui.unit.meters")}";
    }

    private string FormatDegrees(float value)
    {
        return $"{MathF.Round(value)}°";
    }

    private string FormatPercent(float value)
    {
        return $"{MathF.Round(value * 100f)}%";
    }

    private string FormatMinutes(int value)
    {
        return $"{value} {L("ui.unit.minutes")}";
    }

    private string FormatSamples(int value)
    {
        return $"{value}x";
    }

    private string FormatInteger(float value)
    {
        return MathF.Round(value).ToString(CultureInfo.CurrentCulture);
    }

    private string FormatInteger(int value)
    {
        return value.ToString(CultureInfo.CurrentCulture);
    }

    private string FormatNameTagDistance(float value)
    {
        return value <= 0f
            ? L("ui.settings.disabled")
            : $"{MathF.Round(value)} {L("ui.unit.meters")}";
    }
}
