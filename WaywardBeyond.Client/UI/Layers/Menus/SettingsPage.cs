using System;
using System.Threading;
using Reef;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Extensions;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Settings;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Layers.Menus;

/// <summary>Draws and applies one settings page for one menu.</summary>
internal sealed class SettingsPage<TIdentifier>(
    TIdentifier id,
    in ConfigManager configManager,
    in ControlSettings controlSettings,
    in KeybindingSettings keybindingSettings,
    in WindowSettings windowSettings,
    in RenderSettings renderSettings,
    in VolumeSettings volumeSettings,
    in IPlaybackDeviceService playbackDeviceService,
    in GameplayConfig gameplayConfig,
    in UISettings uiSettings,
    in SoundEffectService soundEffectService,
    in IWindowContext windowContext,
    in IShortcutService shortcutService,
    in ILocalization localization,
    bool showDeveloperKeybindings = true
) : IMenuPage<TIdentifier> where TIdentifier : notnull
{
    public TIdentifier ID { get; } = id;

    private readonly ConfigManager _configManager = configManager;
    private readonly ControlSettings _controlSettings = controlSettings;
    private readonly KeybindingSettings _keybindingSettings = keybindingSettings;
    private readonly WindowSettings _windowSettings = windowSettings;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly VolumeSettings _volumeSettings = volumeSettings;
    private readonly IPlaybackDeviceService _playbackDeviceService = playbackDeviceService;
    private readonly GameplayConfig _gameplayConfig = gameplayConfig;
    private readonly UISettings _uiSettings = uiSettings;
    private readonly SoundEffectService _soundEffectService = soundEffectService;
    private readonly IWindowContext _windowContext = windowContext;
    private readonly IShortcutService _shortcutService = shortcutService;
    private readonly ILocalization _localization = localization;
    private readonly bool _showDeveloperKeybindings = showDeveloperKeybindings;
    private readonly Lock _captureLock = new();

    private SettingsShellView? _shell;
    private Menu<TIdentifier>? _menu;
    private double _time;
    private SettingsDraft? _draft;
    private SettingsCategory[]? _categories;
    private SettingsCategory? _keybindingsCategory;
    private ShortcutBinding[] _fixedBindings = [];
    private SettingsCategoryEntry[] _entries = [];
    private SettingsRowState[] _rows = [];
    private bool _rowsDirty = true;
    private int _playbackDevicesDirty;
    private int _externalBindingsDirty;
    private int _categoryIndex;
    private string _statusKey = string.Empty;
    private double _statusUntil;
    private string? _capturingRowId;
    private int _captureGeneration;
    private bool _hasPendingCapture;
    private ShortcutCaptureResult _pendingCapture;
    private int _suppressClicksFrames;

    /// <inheritdoc/>
    public void OnEnter()
    {
        _playbackDeviceService.PlaybackDevicesChanged -= OnPlaybackDevicesChanged;
        _playbackDeviceService.PlaybackDevicesChanged += OnPlaybackDevicesChanged;
        _shortcutService.BindingsChanged -= OnShortcutBindingsChanged;
        _shortcutService.BindingsChanged += OnShortcutBindingsChanged;

        CancelActiveCapture();
        _suppressClicksFrames = 0;
        _draft = SettingsDraft.FromConfigs(
            _controlSettings,
            _windowSettings,
            _renderSettings,
            _volumeSettings,
            _gameplayConfig,
            _uiSettings,
            _windowContext,
            _shortcutService
        );
        _categories = BuildCategories();
        _keybindingsCategory = FindCategory("keybindings");
        _fixedBindings = KeybindingsSettingsCategory.CollectFixedBindings(_shortcutService);
        _entries = BuildEntries(_categories);
        _rowsDirty = true;
        _playbackDevicesDirty = 0;
        _externalBindingsDirty = 0;
        _categoryIndex = 0;
        _statusKey = string.Empty;
        _statusUntil = 0d;
        _shell?.Scroll.Reset();
    }

    /// <inheritdoc/>
    public void OnExit()
    {
        _playbackDeviceService.PlaybackDevicesChanged -= OnPlaybackDevicesChanged;
        _shortcutService.BindingsChanged -= OnShortcutBindingsChanged;
        CancelActiveCapture();
        _suppressClicksFrames = 0;
        _draft = null;
        _categories = null;
        _keybindingsCategory = null;
        _fixedBindings = [];
        _entries = [];
        _rows = [];
        _rowsDirty = true;
        _playbackDevicesDirty = 0;
        _externalBindingsDirty = 0;
        _statusKey = string.Empty;
    }

    /// <inheritdoc/>
    public Result RenderPage(double delta, UIBuilder<Material> ui, Menu<TIdentifier> menu)
    {
        if (_draft == null || _categories == null)
        {
            return Result.FromFailure("The settings page is not prepared.");
        }

        DrainPendingCapture();

        if (RefreshExternalDisplaySettings())
        {
            _rowsDirty = true;
        }

        if (Interlocked.Exchange(ref _playbackDevicesDirty, 0) == 1)
        {
            RefreshPlaybackDeviceSettings();
            _rowsDirty = true;
        }

        if (Interlocked.Exchange(ref _externalBindingsDirty, 0) == 1)
        {
            RefreshExternalKeybindings();
            _rowsDirty = true;
        }

        bool hasConflicts = SyncKeybindingValidation();

        _menu = menu;
        RenderRows(ui);
        _time = ui.Time;

        bool dirty = IsDirty();
        bool capturing = _capturingRowId != null;
        bool canApply = dirty && !hasConflicts && !capturing;
        ResolveStatus(ui.Time, dirty, capturing, hasConflicts, out string statusKey, out bool statusError);

        Shell.Render(ui, new SettingsShellState(
            _entries,
            _categoryIndex,
            _categories[_categoryIndex].TitleKey,
            dirty,
            statusKey,
            dirty ? "ui.button.cancel" : "ui.button.back",
            canApply,
            capturing,
            statusError
        ));

        if (_suppressClicksFrames > 0)
        {
            _suppressClicksFrames--;
        }

        return Result.FromSuccess();
    }

    private void ResolveStatus(
        double time,
        bool dirty,
        bool capturing,
        bool hasConflicts,
        out string statusKey,
        out bool statusError
    )
    {
        if (capturing)
        {
            statusKey = "ui.settings.status.keybindListening";
            statusError = false;
            return;
        }

        if (hasConflicts)
        {
            statusKey = "ui.settings.status.keybindConflict";
            statusError = true;
            return;
        }

        if (dirty)
        {
            statusKey = "ui.settings.status.unsaved";
            statusError = false;
            return;
        }

        statusKey = _statusKey.Length > 0 && time < _statusUntil ? _statusKey : string.Empty;
        statusError = false;
    }

    /// <summary>Gets the shell view, created on first use.</summary>
    internal SettingsShellView Shell => _shell ??= CreateShell();

    private SettingsShellView CreateShell()
    {
        return new SettingsShellView(_localization)
        {
            CategoryRequested = OnCategorySelected,
            CancelRequested = OnCancel,
            ApplyRequested = OnApply,
            ChoiceRequested = OnChoice,
            StepRequested = OnStep,
            SliderDragRequested = OnSliderDrag,
            ResetRequested = OnReset,
            KeybindClickRequested = OnKeybindClick,
            KeybindClearRequested = OnKeybindClear,
            KeybindResetRequested = OnKeybindReset,
        };
    }

    /// <summary>Collects the active category rows and updates the scroll metrics.</summary>
    private void RenderRows(UIBuilder<Material> ui)
    {
        bool useTabs = ui.Width < 900;
        Shell.Scroll.Measure(ui, useTabs ? "Settings.RowsTabs" : "Settings.Rows");

        if (_rowsDirty)
        {
            SettingsRow[] categoryRows = _categories![_categoryIndex].Rows;
            if (_rows.Length != categoryRows.Length)
            {
                _rows = new SettingsRowState[categoryRows.Length];
            }

            for (var i = 0; i < categoryRows.Length; i++)
            {
                _rows[i] = categoryRows[i].Snapshot();
            }

            _rowsDirty = false;
        }

        Shell.Rows = _rows;
    }

    private void Play(WidgetInteraction.InterfaceSound sound)
    {
        if (_soundEffectService is not null)
        {
            WidgetInteraction.Interactions.Click.WithButtonAudio(_soundEffectService, sound);
        }
    }

    private void OnChoice(string id, int index)
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        if (FindEntry(id)?.TrySetOptionIndex(index) == true)
        {
            _rowsDirty = true;
            Play(WidgetInteraction.InterfaceSound.Toggle);
        }
    }

    private void OnStep(string id, int delta)
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        int applied = FindEntry(id)?.Step(delta) ?? 0;
        if (applied != 0)
        {
            _rowsDirty = true;
            Play(applied > 0 ? WidgetInteraction.InterfaceSound.Increase : WidgetInteraction.InterfaceSound.Decrease);
        }
    }

    private void OnSliderDrag(string id, float ratio)
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        int applied = FindEntry(id)?.TrySetSliderRatio(ratio) ?? 0;
        if (applied == 0)
        {
            return;
        }

        _rowsDirty = true;
        Play(applied > 0 ? WidgetInteraction.InterfaceSound.Increase : WidgetInteraction.InterfaceSound.Decrease);
    }

    private void OnKeybindClick(string id)
    {
        if (_suppressClicksFrames > 0 || FindEntry(id) is not KeybindingEntry)
        {
            return;
        }

        CancelActiveCapture();

        int generation;
        lock (_captureLock)
        {
            generation = ++_captureGeneration;
            _hasPendingCapture = false;
            _pendingCapture = default;
        }

        _capturingRowId = id;
        _shortcutService.BeginCapture(result => OnBindingCaptured(generation, result));
        _rowsDirty = true;
        Play(WidgetInteraction.InterfaceSound.Click);
    }

    private void OnKeybindClear(string id)
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        CancelActiveCapture();
        if (FindEntry(id) is not KeybindingEntry entry || entry.IsUnbound)
        {
            return;
        }

        entry.SetValue(ShortcutBinding.Unbound);
        _rowsDirty = true;
        Play(WidgetInteraction.InterfaceSound.Toggle);
    }

    private void OnKeybindReset(string id)
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        CancelActiveCapture();
        if (FindEntry(id) is not KeybindingEntry entry || !entry.CanReset)
        {
            return;
        }

        entry.ResetToDefault();
        _rowsDirty = true;
        Play(WidgetInteraction.InterfaceSound.Toggle);
    }

    private void OnReset(string id)
    {
        if (_suppressClicksFrames > 0 || _categories == null)
        {
            return;
        }

        CancelActiveCapture();
        for (var i = 0; i < _categories.Length; i++)
        {
            SettingsRow[] rows = _categories[i].Rows;
            for (var j = 0; j < rows.Length; j++)
            {
                if (rows[j] is SettingsResetRow reset
                    && string.Equals(reset.Id, id, StringComparison.Ordinal))
                {
                    reset.Invoke();
                    _rowsDirty = true;
                    Play(WidgetInteraction.InterfaceSound.Click);
                    return;
                }
            }
        }
    }

    private void OnCategorySelected(int index)
    {
        if (_suppressClicksFrames > 0 || index == _categoryIndex || _categories == null)
        {
            return;
        }

        CancelActiveCapture();
        _categoryIndex = index;
        _rowsDirty = true;
        _shell?.Scroll.Reset();
        Play(WidgetInteraction.InterfaceSound.Toggle);
    }

    private void OnCancel()
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        CancelActiveCapture();
        Play(WidgetInteraction.InterfaceSound.Click);
        _menu?.GoBack();
    }

    private void OnApply()
    {
        if (_suppressClicksFrames > 0)
        {
            return;
        }

        ApplySettings(_time);
    }

    private void OnBindingCaptured(int generation, ShortcutCaptureResult result)
    {
        lock (_captureLock)
        {
            if (generation != _captureGeneration)
            {
                return;
            }

            _pendingCapture = result;
            _hasPendingCapture = true;
        }
    }

    private void DrainPendingCapture()
    {
        ShortcutCaptureResult captured;
        bool hasCapture;
        lock (_captureLock)
        {
            hasCapture = _hasPendingCapture;
            captured = _pendingCapture;
            _hasPendingCapture = false;
            _pendingCapture = default;
        }

        if (!hasCapture)
        {
            return;
        }

        string? rowId = _capturingRowId;
        _capturingRowId = null;
        _rowsDirty = true;
        //  Reef reports clicks on the frame after UpdateInteraction sees the press, so suppress two frames for mouse capture.
        _suppressClicksFrames = Math.Max(_suppressClicksFrames, !captured.Cancelled && captured.Binding.IsMouseButton ? 2 : 1);

        if (!captured.Cancelled && rowId != null && FindEntry(rowId) is KeybindingEntry entry)
        {
            entry.SetValue(captured.Binding);
            Play(WidgetInteraction.InterfaceSound.Toggle);
        }
        else
        {
            Play(WidgetInteraction.InterfaceSound.Click);
        }
    }

    private void CancelActiveCapture()
    {
        bool wasCapturing = _capturingRowId != null;
        _capturingRowId = null;

        lock (_captureLock)
        {
            _captureGeneration++;
            _hasPendingCapture = false;
            _pendingCapture = default;
        }

        if (wasCapturing || _shortcutService.IsCapturing)
        {
            _shortcutService.CancelCapture();
            _rowsDirty = true;
        }
    }

    private bool SyncKeybindingValidation()
    {
        if (_keybindingsCategory == null)
        {
            return false;
        }

        SettingsEntry[] entries = _keybindingsCategory.Entries;
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i] is KeybindingEntry entry)
            {
                entry.IsCapturing = string.Equals(entry.Id, _capturingRowId, StringComparison.Ordinal);
            }
        }

        return KeybindingsSettingsCategory.UpdateConflicts(_keybindingsCategory, _fixedBindings);
    }

    private void ApplySettings(double time)
    {
        SettingsDraft draft = _draft ?? throw new InvalidOperationException("The settings draft is not initialized.");

        bool hasConflicts = SyncKeybindingValidation();
        if (hasConflicts || _capturingRowId != null)
        {
            return;
        }

        Result displayResult = Result.FromSuccess();
        bool displayChanged = IsEntryDirty("Settings.DisplayMode")
            || IsEntryDirty("Settings.Monitor")
            || IsEntryDirty("Settings.Resolution");
        if (displayChanged)
        {
            displayResult = _windowContext.ApplyDisplaySettings(
                draft.DisplayMode.ToWindowMode(),
                draft.DisplayMode.IsBorderless(),
                draft.Resolution.Width,
                draft.Resolution.Height,
                draft.Monitor
            );

            if (!displayResult.Success)
            {
                draft.DisplayMode = SettingsDisplayModeExtensions.FromWindowSettings(
                    _windowSettings.Mode.Get(),
                    _windowSettings.Borderless.Get()
                );
                draft.Monitor = _windowSettings.Monitor.Get() ?? string.Empty;
                draft.Resolution = SettingsDraft.GetWindowResolution(_windowSettings, _windowContext);
            }
        }

        _renderSettings.Reef.Scale.Set(draft.UiScale);
        _renderSettings.VSync.Set(draft.VSync);
        _renderSettings.Framerate.Set(draft.FrameLimit);
        _renderSettings.FOV.Set(draft.Fov);
        _renderSettings.FarPlane.Set(draft.ViewDistance);
        _renderSettings.AntiAliasing.Set(draft.AntiAliasingMode);
        draft.ShadowQuality.Apply(_renderSettings);
        _renderSettings.SAO.Enabled.Set(draft.AmbientOcclusion);
        draft.AoQuality.Apply(_renderSettings);
        _renderSettings.Bloom.Enabled.Set(draft.Bloom);
        draft.BloomQuality.Apply(_renderSettings);
        draft.ParticleDensity.Apply(_renderSettings);

        _volumeSettings.Master.Set(draft.MasterVolume);
        _volumeSettings.Music.Set(draft.MusicVolume);
        _volumeSettings.Effects.Set(draft.EffectsVolume);
        _volumeSettings.Interface.Set(draft.InterfaceVolume);
        _volumeSettings.Paused.Set(draft.PausedVolume);
        _volumeSettings.Background.Set(draft.BackgroundVolume);
        _volumeSettings.OutputDevice.Set(draft.OutputDevice);
        _playbackDeviceService.SetPreferredPlaybackDevice(draft.OutputDevice);

        _gameplayConfig.Autosave.Set(draft.Autosave);
        _gameplayConfig.AutosaveIntervalMs.Set(draft.AutosaveIntervalMinutes * 60000);
        _gameplayConfig.ControlHints.Set(draft.ControlHints);
        _gameplayConfig.Crosshair.Set(draft.Crosshair);

        _uiSettings.NameplateDistance.Set(draft.NameTagDistance);

        _controlSettings.LookSensitivity.Set(draft.MouseSensitivity);
        _controlSettings.RememberShape.Set(draft.RememberShape);
        _controlSettings.RememberOrientation.Set(draft.RememberOrientation);

        if (_keybindingsCategory != null)
        {
            SettingsEntry[] keybindEntries = _keybindingsCategory.Entries;
            for (var i = 0; i < keybindEntries.Length; i++)
            {
                if (keybindEntries[i] is KeybindingEntry entry && entry.IsDirty)
                {
                    _shortcutService.Rebind(entry.ShortcutId, entry.Value);
                }
            }
        }

        _keybindingSettings?.SetOverrides(_shortcutService.GetOverrides());

        if (_configManager != null)
        {
            _configManager.ApplySettings();
        }

        if (_categories != null)
        {
            for (var i = 0; i < _categories.Length; i++)
            {
                _categories[i].CaptureBaselines();
            }
        }

        if (displayResult.Success)
        {
            _statusKey = "ui.settings.status.saved";
            _statusUntil = time + 2d;
        }
        else
        {
            _statusKey = "ui.settings.status.displayFallback";
            _statusUntil = time + 4d;
        }

        _rowsDirty = true;
        Play(WidgetInteraction.InterfaceSound.Click);
    }

    private static SettingsCategoryEntry[] BuildEntries(SettingsCategory[] categories)
    {
        var entries = new SettingsCategoryEntry[categories.Length];
        for (var i = 0; i < categories.Length; i++)
        {
            entries[i] = new SettingsCategoryEntry(categories[i].Id, categories[i].TitleKey);
        }

        return entries;
    }

    private SettingsCategory[] BuildCategories()
    {
        SettingsDraft draft = _draft ?? throw new InvalidOperationException("The settings draft is not initialized.");

        return
        [
            DisplaySettingsCategory.Build(draft, _windowContext, _localization, () => ResetCategory("display")),
            AudioSettingsCategory.Build(draft, _playbackDeviceService, _localization, () => ResetCategory("audio")),
            GameplaySettingsCategory.Build(draft, _localization, () => ResetCategory("gameplay")),
            ControlSettingsCategory.Build(draft, _localization, () => ResetCategory("controls")),
            KeybindingsSettingsCategory.Build(draft, _shortcutService, () => ResetCategory("keybindings"), _showDeveloperKeybindings),
        ];
    }

    private SettingsCategory? FindCategory(string categoryId)
    {
        if (_categories == null)
        {
            return null;
        }

        for (var i = 0; i < _categories.Length; i++)
        {
            if (_categories[i].Id == categoryId)
            {
                return _categories[i];
            }
        }

        return null;
    }

    private void OnPlaybackDevicesChanged(object? sender, PlaybackDevicesChangedEventArgs e)
    {
        Interlocked.Exchange(ref _playbackDevicesDirty, 1);
    }

    private void OnShortcutBindingsChanged()
    {
        Interlocked.Exchange(ref _externalBindingsDirty, 1);
    }

    private void RefreshExternalKeybindings()
    {
        if (_draft == null || _keybindingsCategory == null)
        {
            return;
        }

        _fixedBindings = KeybindingsSettingsCategory.CollectFixedBindings(_shortcutService);

        SettingsEntry[] entries = _keybindingsCategory.Entries;
        for (var i = 0; i < entries.Length; i++)
        {
            if (entries[i] is not KeybindingEntry entry || entry.IsDirty)
            {
                continue;
            }

            Result<ShortcutBinding> live = _shortcutService.GetBinding(entry.ShortcutId);
            if (!live.Success || entry.Value == live.Value)
            {
                continue;
            }

            entry.SetValue(live.Value);
            entry.CaptureBaseline();
        }
    }

    private void RefreshPlaybackDeviceSettings()
    {
        if (_draft == null)
        {
            return;
        }

        SettingsEntry? entry = FindEntry("Settings.OutputDevice");
        if (entry == null || entry.IsDirty)
        {
            return;
        }

        string preferredDevice = _playbackDeviceService.PreferredPlaybackDeviceName ?? string.Empty;
        if (string.Equals(_draft.OutputDevice, preferredDevice, StringComparison.Ordinal))
        {
            return;
        }

        _draft.OutputDevice = preferredDevice;
        entry.CaptureBaseline();
    }

    private bool RefreshExternalDisplaySettings()
    {
        if (_draft == null)
        {
            return false;
        }

        var changed = false;
        SettingsEntry? displayModeEntry = FindEntry("Settings.DisplayMode");
        SettingsDisplayMode currentMode = SettingsDisplayModeExtensions.FromWindowSettings(
            _windowSettings.Mode.Get(),
            _windowSettings.Borderless.Get()
        );

        if (displayModeEntry != null && !displayModeEntry.IsDirty && _draft.DisplayMode != currentMode)
        {
            _draft.DisplayMode = currentMode;
            displayModeEntry.CaptureBaseline();
            changed = true;
        }

        SettingsEntry? monitorEntry = FindEntry("Settings.Monitor");
        string currentMonitor = _windowSettings.Monitor.Get() ?? string.Empty;

        if (monitorEntry != null && !monitorEntry.IsDirty && !string.Equals(_draft.Monitor, currentMonitor, StringComparison.Ordinal))
        {
            _draft.Monitor = currentMonitor;
            monitorEntry.CaptureBaseline();
            changed = true;
        }

        SettingsEntry? resolutionEntry = FindEntry("Settings.Resolution");
        SettingsResolution currentResolution = SettingsDraft.GetWindowResolution(_windowSettings, _windowContext);

        if (resolutionEntry != null && !resolutionEntry.IsDirty && _draft.Resolution != currentResolution)
        {
            _draft.Resolution = currentResolution;
            resolutionEntry.CaptureBaseline();
            changed = true;
        }

        return changed;
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
}
