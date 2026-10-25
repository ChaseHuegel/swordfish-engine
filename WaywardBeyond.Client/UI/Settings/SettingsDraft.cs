using System;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Settings;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Config;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Editable settings values for one settings page session.</summary>
internal sealed class SettingsDraft
{
    public SettingsDisplayMode DisplayMode;
    public SettingsResolution Resolution;
    public bool VSync;
    public int FrameLimit;
    public int Fov;
    public float ViewDistance;
    public AntiAliasing AntiAliasingMode;
    public bool AmbientOcclusion;
    public int AoSamples;
    public bool Bloom;
    public int BloomPasses;
    public float MasterVolume;
    public float MusicVolume;
    public float EffectsVolume;
    public float InterfaceVolume;
    public bool Autosave;
    public int AutosaveIntervalMinutes;
    public bool ControlHints;
    public bool Crosshair;
    public int NameTagDistance;
    public int MouseSensitivity;
    public bool RememberShape;
    public bool RememberOrientation;

    /// <summary>Copies the current configuration into a draft.</summary>
    public static SettingsDraft FromConfigs(
        in ControlSettings controlSettings,
        in WindowSettings windowSettings,
        in RenderSettings renderSettings,
        in VolumeSettings volumeSettings,
        in GameplayConfig gameplayConfig,
        in UISettings uiSettings,
        in IWindowContext windowContext
    )
    {
        return new SettingsDraft
        {
            DisplayMode = SettingsDisplayModeExtensions.FromWindowSettings(
                windowSettings.Mode.Get(),
                windowSettings.Borderless.Get()
            ),
            Resolution = GetWindowResolution(windowSettings, windowContext),
            VSync = renderSettings.VSync.Get(),
            FrameLimit = renderSettings.Framerate.Get(),
            Fov = renderSettings.FOV.Get(),
            ViewDistance = renderSettings.FarPlane.Get(),
            AntiAliasingMode = renderSettings.AntiAliasing.Get(),
            AmbientOcclusion = renderSettings.SAO.Enabled.Get(),
            AoSamples = renderSettings.SAO.Samples.Get(),
            Bloom = renderSettings.Bloom.Enabled.Get(),
            BloomPasses = renderSettings.Bloom.Passes.Get(),
            MasterVolume = volumeSettings.Master.Get(),
            MusicVolume = volumeSettings.Music.Get(),
            EffectsVolume = volumeSettings.Effects.Get(),
            InterfaceVolume = volumeSettings.Interface.Get(),
            Autosave = gameplayConfig.Autosave.Get(),
            AutosaveIntervalMinutes = Math.Max(1, (int)Math.Round(gameplayConfig.AutosaveIntervalMs.Get() / 60000d)),
            ControlHints = gameplayConfig.ControlHints.Get(),
            Crosshair = gameplayConfig.Crosshair.Get(),
            NameTagDistance = uiSettings.NameplateDistance.Get(),
            MouseSensitivity = controlSettings.LookSensitivity.Get(),
            RememberShape = controlSettings.RememberShape.Get(),
            RememberOrientation = controlSettings.RememberOrientation.Get(),
        };
    }

    /// <summary>Creates a draft with the settings page's recommended defaults.</summary>
    public static SettingsDraft CreateDefault(IWindowContext windowContext)
    {
        return new SettingsDraft
        {
            DisplayMode = SettingsDefaults.DISPLAY_MODE,
            Resolution = GetNativeResolution(windowContext),
            VSync = SettingsDefaults.V_SYNC,
            FrameLimit = SettingsDefaults.FRAME_LIMIT,
            Fov = SettingsDefaults.FOV,
            ViewDistance = SettingsDefaults.VIEW_DISTANCE,
            AntiAliasingMode = SettingsDefaults.ANTI_ALIASING_MODE,
            AmbientOcclusion = SettingsDefaults.AMBIENT_OCCLUSION,
            AoSamples = SettingsDefaults.AO_SAMPLES,
            Bloom = SettingsDefaults.BLOOM,
            BloomPasses = SettingsDefaults.BLOOM_PASSES,
            MasterVolume = SettingsDefaults.MASTER_VOLUME,
            MusicVolume = SettingsDefaults.MUSIC_VOLUME,
            EffectsVolume = SettingsDefaults.EFFECTS_VOLUME,
            InterfaceVolume = SettingsDefaults.INTERFACE_VOLUME,
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
        if (windowSettings.Mode.Get() == WindowMode.Windowed)
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
