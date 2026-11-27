using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Editable settings values for one settings page session.</summary>
internal sealed class SettingsDraft
{
    public SettingsDisplayMode DisplayMode;
    public string Monitor = string.Empty;
    public SettingsResolution Resolution;
    public float UiScale;
    public bool VSync;
    public int FrameLimit;
    public int Fov;
    public float ViewDistance;
    public AntiAliasing AntiAliasingMode;
    public ShadowQuality ShadowQuality;
    public bool AmbientOcclusion;
    public AoQuality AoQuality;
    public bool Bloom;
    public BloomQuality BloomQuality;
    public ParticleDensity ParticleDensity;
    public float MasterVolume;
    public float MusicVolume;
    public float EffectsVolume;
    public float InterfaceVolume;
    public float PausedVolume;
    public float BackgroundVolume;
    public string OutputDevice = string.Empty;
    public bool Autosave;
    public int AutosaveIntervalMinutes;
    public bool ControlHints;
    public bool Crosshair;
    public int NameTagDistance;
    public int MouseSensitivity;
    public bool RememberShape;
    public bool RememberOrientation;
    public Dictionary<string, ShortcutBinding> Keybindings { get; } = new(StringComparer.Ordinal);

    /// <summary>Copies the current configuration into a draft.</summary>
    public static SettingsDraft FromConfigs(
        in ControlSettings controlSettings,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in VolumeSettings volumeSettings,
        in GameplayConfig gameplayConfig,
        in UISettings uiSettings,
        in IWindowContext windowContext,
        in IShortcutService shortcutService
    )
    {
        var draft = new SettingsDraft
        {
            DisplayMode = SettingsDisplayModeExtensions.FromWindowSettings(
                windowSettings.Mode.Get(),
                windowSettings.Borderless.Get()
            ),
            Monitor = windowSettings.Monitor.Get() ?? string.Empty,
            Resolution = GetWindowResolution(windowSettings, windowContext),
            UiScale = renderSettings.Reef.Scale.Get(),
            VSync = renderSettings.VSync.Get(),
            FrameLimit = renderSettings.Framerate.Get(),
            Fov = renderSettings.FOV.Get(),
            ViewDistance = renderSettings.FarPlane.Get(),
            AntiAliasingMode = renderSettings.AntiAliasing.Get(),
            ShadowQuality = ShadowQualityExtensions.FromSettings(renderSettings.Shadows),
            AmbientOcclusion = renderSettings.SAO.Enabled.Get(),
            AoQuality = AoQualityExtensions.FromSettings(renderSettings.SAO),
            Bloom = renderSettings.Bloom.Enabled.Get(),
            BloomQuality = BloomQualityExtensions.FromSettings(renderSettings.Bloom),
            ParticleDensity = ParticleDensityExtensions.FromSettings(renderSettings.Particles),
            MasterVolume = volumeSettings.Master.Get(),
            MusicVolume = volumeSettings.Music.Get(),
            EffectsVolume = volumeSettings.Effects.Get(),
            InterfaceVolume = volumeSettings.Interface.Get(),
            PausedVolume = volumeSettings.Paused.Get(),
            BackgroundVolume = volumeSettings.Background.Get(),
            OutputDevice = volumeSettings.OutputDevice.Get() ?? string.Empty,
            Autosave = gameplayConfig.Autosave.Get(),
            AutosaveIntervalMinutes = Math.Max(1, (int)Math.Round(gameplayConfig.AutosaveIntervalMs.Get() / 60000d)),
            ControlHints = gameplayConfig.ControlHints.Get(),
            Crosshair = gameplayConfig.Crosshair.Get(),
            NameTagDistance = uiSettings.NameplateDistance.Get(),
            MouseSensitivity = controlSettings.LookSensitivity.Get(),
            RememberShape = controlSettings.RememberShape.Get(),
            RememberOrientation = controlSettings.RememberOrientation.Get(),
        };

        IReadOnlyList<ShortcutInfo> shortcuts = shortcutService.GetShortcuts();
        for (var i = 0; i < shortcuts.Count; i++)
        {
            ShortcutInfo info = shortcuts[i];
            if (info.Rebindable)
            {
                draft.Keybindings[info.Id] = info.CurrentBinding;
            }
        }

        return draft;
    }

    /// <summary>Creates a draft with the settings page's recommended defaults.</summary>
    public static SettingsDraft CreateDefault(IWindowContext windowContext)
    {
        return new SettingsDraft
        {
            DisplayMode = SettingsDefaults.DISPLAY_MODE,
            Monitor = SettingsDefaults.MONITOR,
            Resolution = GetNativeResolution(windowContext),
            UiScale = SettingsDefaults.UI_SCALE,
            VSync = SettingsDefaults.V_SYNC,
            FrameLimit = SettingsDefaults.FRAME_LIMIT,
            Fov = SettingsDefaults.FOV,
            ViewDistance = SettingsDefaults.VIEW_DISTANCE,
            AntiAliasingMode = SettingsDefaults.ANTI_ALIASING_MODE,
            ShadowQuality = SettingsDefaults.SHADOW_QUALITY,
            AmbientOcclusion = SettingsDefaults.AMBIENT_OCCLUSION,
            AoQuality = SettingsDefaults.AO_QUALITY,
            Bloom = SettingsDefaults.BLOOM,
            BloomQuality = SettingsDefaults.BLOOM_QUALITY,
            ParticleDensity = SettingsDefaults.PARTICLE_DENSITY,
            MasterVolume = SettingsDefaults.MASTER_VOLUME,
            MusicVolume = SettingsDefaults.MUSIC_VOLUME,
            EffectsVolume = SettingsDefaults.EFFECTS_VOLUME,
            InterfaceVolume = SettingsDefaults.INTERFACE_VOLUME,
            PausedVolume = SettingsDefaults.PAUSED_VOLUME,
            BackgroundVolume = SettingsDefaults.BACKGROUND_VOLUME,
            OutputDevice = SettingsDefaults.OUTPUT_DEVICE,
            Autosave = SettingsDefaults.AUTOSAVE,
            AutosaveIntervalMinutes = SettingsDefaults.AUTOSAVE_INTERVAL_MINUTES,
            ControlHints = SettingsDefaults.CONTROL_HINTS,
            Crosshair = SettingsDefaults.CROSSHAIR,
            NameTagDistance = SettingsDefaults.NAME_TAG_DISTANCE,
            MouseSensitivity = SettingsDefaults.MOUSE_SENSITIVITY,
            RememberShape = SettingsDefaults.REMEMBER_SHAPE,
            RememberOrientation = SettingsDefaults.REMEMBER_ORIENTATION,
        };
    }

    /// <summary>Gets the current window resolution, falling back to the monitor.</summary>
    public static SettingsResolution GetWindowResolution(in WindowSettings windowSettings, in IWindowContext windowContext)
    {
        WindowMode mode = windowSettings.Mode.Get();
        if (mode == WindowMode.Fullscreen && !windowSettings.Borderless.Get())
        {
            int? fullscreenWidth = windowSettings.FullscreenWidth.Get();
            int? fullscreenHeight = windowSettings.FullscreenHeight.Get();
            if (fullscreenWidth > 0 && fullscreenHeight > 0)
            {
                return new SettingsResolution(fullscreenWidth.Value, fullscreenHeight.Value);
            }

            Vector2 fullscreenSize = windowContext.Resolution;
            if (fullscreenSize.X > 0f && fullscreenSize.Y > 0f)
            {
                return new SettingsResolution((int)fullscreenSize.X, (int)fullscreenSize.Y);
            }

            return GetNativeResolution(windowContext);
        }

        if (mode == WindowMode.Windowed)
        {
            Vector2 currentSize = windowContext.Resolution;
            if (currentSize.X > 0f && currentSize.Y > 0f)
            {
                return new SettingsResolution((int)currentSize.X, (int)currentSize.Y);
            }
        }

        int width = windowSettings.Width.Get() ?? (int)windowContext.Resolution.X;
        int height = windowSettings.Height.Get() ?? (int)windowContext.Resolution.Y;

        if (width <= 0 || height <= 0)
        {
            return GetNativeResolution(windowContext);
        }

        return new SettingsResolution(width, height);
    }

    /// <summary>Gets the current monitor resolution, falling back to the window.</summary>
    public static SettingsResolution GetNativeResolution(in IWindowContext windowContext)
    {
        Vector2 monitorResolution = windowContext.MonitorResolution;
        if (monitorResolution.X > 0f && monitorResolution.Y > 0f)
        {
            return new SettingsResolution((int)monitorResolution.X, (int)monitorResolution.Y);
        }

        Vector2 windowResolution = windowContext.Resolution;
        if (windowResolution.X > 0f && windowResolution.Y > 0f)
        {
            return new SettingsResolution((int)windowResolution.X, (int)windowResolution.Y);
        }

        return new SettingsResolution(1920, 1080);
    }
}
