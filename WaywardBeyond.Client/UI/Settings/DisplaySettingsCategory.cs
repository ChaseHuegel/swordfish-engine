using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Swordfish.Graphics;
using Swordfish.Library.Globalization;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Builds the display settings category.</summary>
internal static class DisplaySettingsCategory
{
    private static readonly SettingsDisplayMode[] _displayModes =
    [
        SettingsDisplayMode.Windowed,
        SettingsDisplayMode.BorderlessWindow,
        SettingsDisplayMode.Maximized,
        SettingsDisplayMode.BorderlessMaximized,
        SettingsDisplayMode.Fullscreen,
        SettingsDisplayMode.BorderlessFullscreen,
    ];

    private static readonly AntiAliasing[] _antiAliasingOptions =
    [
        AntiAliasing.None,
        AntiAliasing.MSAA,
    ];

    public static SettingsCategory Build(SettingsDraft draft, IWindowContext windowContext, ILocalization localization, Action reset)
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
                    get: () => draft.DisplayMode,
                    set: value => draft.DisplayMode = value,
                    defaultValue: () => SettingsDefaults.DISPLAY_MODE,
                    options: _displayModes,
                    format: value => FormatDisplayMode(localization, value)
                ),
                new StepperEntry<SettingsResolution>(
                    id: "Settings.Resolution",
                    labelKey: "ui.settings.resolution",
                    get: () => draft.Resolution,
                    set: value => draft.Resolution = value,
                    defaultValue: () => SettingsDraft.GetNativeResolution(windowContext),
                    options: GetResolutionOptions(draft, windowContext),
                    format: resolution => $"{resolution.Width} x {resolution.Height}",
                    isEnabled: () => draft.DisplayMode.UsesWindowSize()
                ),
                new ToggleEntry(
                    id: "Settings.VSync",
                    labelKey: "ui.settings.vsync",
                    get: () => draft.VSync,
                    set: value => draft.VSync = value,
                    defaultValue: () => SettingsDefaults.V_SYNC
                ),
                new StepperEntry<int>(
                    id: "Settings.FrameLimit",
                    labelKey: "ui.settings.frameLimit",
                    get: () => draft.FrameLimit,
                    set: value => draft.FrameLimit = value,
                    defaultValue: () => SettingsDefaults.FRAME_LIMIT,
                    options: SettingsDefaults.GetFrameLimits(draft.FrameLimit),
                    format: value => FormatFrameLimit(localization, value),
                    isEnabled: () => !draft.VSync
                ),
                new SettingsSection("ui.settings.section.view"),
                new SliderEntry(
                    id: "Settings.Fov",
                    labelKey: "ui.settings.fov",
                    get: () => draft.Fov,
                    set: value => draft.Fov = (int)MathF.Round(value),
                    defaultValue: () => SettingsDefaults.FOV,
                    min: SettingsDefaults.FOV_MIN,
                    max: SettingsDefaults.FOV_MAX,
                    step: 1f,
                    format: FormatDegrees
                ),
                new StepperEntry<float>(
                    id: "Settings.ViewDistance",
                    labelKey: "ui.settings.viewDistance",
                    get: () => draft.ViewDistance,
                    set: value => draft.ViewDistance = value,
                    defaultValue: () => SettingsDefaults.VIEW_DISTANCE,
                    options: SettingsDefaults.GetViewDistances(draft.ViewDistance),
                    format: value => FormatDistance(localization, value)
                ),
                new SettingsSection("ui.settings.section.quality"),
                new ChoiceEntry<AntiAliasing>(
                    id: "Settings.AntiAliasing",
                    labelKey: "ui.settings.antiAliasing",
                    get: () => draft.AntiAliasingMode,
                    set: value => draft.AntiAliasingMode = value,
                    defaultValue: () => SettingsDefaults.ANTI_ALIASING_MODE,
                    options: _antiAliasingOptions,
                    optionLabel: value => FormatAntiAliasing(localization, value)
                ),
                new ToggleEntry(
                    id: "Settings.AmbientOcclusion",
                    labelKey: "ui.settings.ambientOcclusion",
                    get: () => draft.AmbientOcclusion,
                    set: value => draft.AmbientOcclusion = value,
                    defaultValue: () => SettingsDefaults.AMBIENT_OCCLUSION
                ),
                new StepperEntry<int>(
                    id: "Settings.AoSamples",
                    labelKey: "ui.settings.aoSamples",
                    get: () => draft.AoSamples,
                    set: value => draft.AoSamples = value,
                    defaultValue: () => SettingsDefaults.AO_SAMPLES,
                    options: SettingsDefaults.GetAoSamples(draft.AoSamples),
                    format: FormatSamples,
                    isEnabled: () => draft.AmbientOcclusion
                ),
                new ToggleEntry(
                    id: "Settings.Bloom",
                    labelKey: "ui.settings.bloom",
                    get: () => draft.Bloom,
                    set: value => draft.Bloom = value,
                    defaultValue: () => SettingsDefaults.BLOOM
                ),
                new StepperEntry<int>(
                    id: "Settings.BloomPasses",
                    labelKey: "ui.settings.bloomPasses",
                    get: () => draft.BloomPasses,
                    set: value => draft.BloomPasses = value,
                    defaultValue: () => SettingsDefaults.BLOOM_PASSES,
                    options: SettingsDefaults.GetBloomPasses(draft.BloomPasses),
                    format: FormatInteger,
                    isEnabled: () => draft.Bloom
                ),
                new SettingsResetRow("Settings.Reset.display", reset),
            ]
        );
    }

    private static IReadOnlyList<SettingsResolution> GetResolutionOptions(
        SettingsDraft draft,
        IWindowContext windowContext
    )
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

        Add(draft.Resolution, alwaysInclude: true);

        IReadOnlyList<Vector2> videoModes = windowContext.GetVideoModes();
        for (var i = 0; i < videoModes.Count; i++)
        {
            Vector2 mode = videoModes[i];
            Add(new SettingsResolution((int)mode.X, (int)mode.Y));
        }

        Add(SettingsDraft.GetNativeResolution(windowContext), alwaysInclude: true);

        options.Sort(static (left, right) =>
        {
            int widthComparison = left.Width.CompareTo(right.Width);
            return widthComparison != 0 ? widthComparison : left.Height.CompareTo(right.Height);
        });

        return options;
    }

    private static string FormatDisplayMode(ILocalization localization, SettingsDisplayMode mode)
    {
        return mode switch
        {
            SettingsDisplayMode.Windowed => L(localization, "ui.settings.displayMode.windowed"),
            SettingsDisplayMode.BorderlessWindow => L(localization, "ui.settings.displayMode.borderlessWindow"),
            SettingsDisplayMode.Maximized => L(localization, "ui.settings.displayMode.maximized"),
            SettingsDisplayMode.BorderlessMaximized => L(localization, "ui.settings.displayMode.borderlessMaximized"),
            SettingsDisplayMode.Fullscreen => L(localization, "ui.settings.displayMode.fullscreen"),
            SettingsDisplayMode.BorderlessFullscreen => L(localization, "ui.settings.displayMode.borderlessFullscreen"),
            _ => mode.ToString(),
        };
    }

    private static string FormatFrameLimit(ILocalization localization, int value)
    {
        if (value == 0)
        {
            return L(localization, "ui.settings.unlimited");
        }

        string unit = L(localization, "ui.unit.fps");
        return $"{value} {unit}";
    }

    private static string FormatDegrees(float value)
    {
        return $"{MathF.Round(value)}°";
    }

    private static string FormatDistance(ILocalization localization, float value)
    {
        if (value >= 1000f)
        {
            string kilometers = L(localization, "ui.unit.kilometers");
            return $"{value / 1000f:0.#} {kilometers}";
        }

        string meters = L(localization, "ui.unit.meters");
        return $"{MathF.Round(value)} {meters}";
    }

    private static string FormatAntiAliasing(ILocalization localization, AntiAliasing antiAliasing)
    {
        return antiAliasing == AntiAliasing.MSAA
            ? L(localization, "ui.settings.antiAliasing.msaa")
            : L(localization, "ui.settings.antiAliasing.off");
    }

    private static string FormatSamples(int value)
    {
        return $"{value}x";
    }

    private static string FormatInteger(int value)
    {
        return value.ToString(CultureInfo.CurrentCulture);
    }

    private static string L(ILocalization localization, string key)
    {
        return localization.GetString(key) ?? key;
    }
}
