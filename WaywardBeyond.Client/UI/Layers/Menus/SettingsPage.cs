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
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Settings;
using WaywardBeyond.Client.UI.Theme;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Draws and applies one settings page for one menu.</summary>
internal sealed class SettingsPage<TIdentifier>(
    TIdentifier id,
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
    public TIdentifier ID { get; } = id;

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

    private readonly SettingsRenderer _renderer = new(localization, soundEffectService, inputService);
    private SettingsShellView? _shell;
    private Menu<TIdentifier>? _menu;
    private double _time;
    private SettingsDraft? _draft;
    private SettingsCategory[]? _categories;
    private int _categoryIndex;
    private int _scrollY;
    private string? _status;
    private double _statusUntil;

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
            return Result.FromFailure("The settings page is not prepared.");
        }

        RefreshExternalDisplaySettings();

        SettingsLayout layout = SettingsLayout.Compute(ui.Width, ui.Height);
        _renderer.SetLayout(ui, layout);
        _menu = menu;
        Shell.Body = DrawRows;
        _time = ui.Time;

        bool dirty = IsDirty();
        string? status = _status != null && ui.Time < _statusUntil ? _status : null;

        var entries = new SettingsCategoryEntry[_categories.Length];
        for (var i = 0; i < _categories.Length; i++)
        {
            entries[i] = new SettingsCategoryEntry(
                _categories[i].Id,
                _localization.GetRequired(_categories[i].TitleKey)
            );
        }

        Shell.Render(ui, new SettingsShellState(
            layout.UseTabs,
            entries,
            _categoryIndex,
            _localization.GetRequired(_categories[_categoryIndex].TitleKey),
            dirty,
            dirty ? _localization.GetRequired("ui.settings.status.unsaved") : status,
            dirty ? _localization.GetRequired("ui.button.cancel") : _localization.GetRequired("ui.button.back")
        ));

        return Result.FromSuccess();
    }

    private SettingsShellView Shell => _shell ??= CreateShell();

    private SettingsShellView CreateShell()
    {
        return new SettingsShellView(_localization)
        {
            CategoryRequested = OnCategorySelected,
            CancelRequested = OnCancel,
            ApplyRequested = OnApply,
        };
    }

    /// <summary>Draws the rows of the active category inside the shell.</summary>
    private void DrawRows(UIBuilder<Material> ui)
    {
        _renderer.DrawRows(_categories![_categoryIndex], ref _scrollY);
    }

    private void OnCategorySelected(int index)
    {
        if (index == _categoryIndex || _categories == null)
        {
            return;
        }

        _categoryIndex = index;
        _scrollY = 0;
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Toggle);
    }

    private void OnCancel()
    {
        _soundEffectService.PlayInterfaceSound(Widgets.InterfaceSound.Click);
        _menu?.GoBack();
    }

    private void OnApply()
    {
        ApplySettings(_time);
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
        draft.ShadowQuality.Apply(_renderSettings);
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

        _status = L("ui.settings.status.saved");
        _statusUntil = time + 2d;
        Widgets.Interactions.Click.WithButtonAudio(_soundEffectService);
    }

    private SettingsCategory[] BuildCategories()
    {
        SettingsDraft draft = _draft ?? throw new InvalidOperationException("The settings draft is not initialized.");

        return
        [
            DisplaySettingsCategory.Build(draft, _windowContext, _localization, () => ResetCategory("display")),
            AudioSettingsCategory.Build(draft, _localization, () => ResetCategory("audio")),
            GameplaySettingsCategory.Build(draft, _localization, () => ResetCategory("gameplay")),
            ControlSettingsCategory.Build(draft, _localization, () => ResetCategory("controls")),
        ];
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

    private string L(string key)
    {
        return _localization.GetString(key) ?? key;
    }
}
