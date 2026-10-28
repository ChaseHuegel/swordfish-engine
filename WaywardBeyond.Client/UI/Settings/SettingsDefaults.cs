using System.Collections.Generic;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Recommended defaults for the settings page.</summary>
internal static class SettingsDefaults
{
    public const SettingsDisplayMode DISPLAY_MODE = SettingsDisplayMode.Windowed;
    public const bool V_SYNC = false;
    public const int FRAME_LIMIT = 0;
    public const int FOV = 90;
    public const float VIEW_DISTANCE = 1000f;
    public const AntiAliasing ANTI_ALIASING_MODE = AntiAliasing.MSAA;
    public const ShadowQuality SHADOW_QUALITY = ShadowQuality.Medium;
    public const bool AMBIENT_OCCLUSION = true;
    public const int AO_SAMPLES = 8;
    public const bool BLOOM = true;
    public const int BLOOM_PASSES = 10;
    public const float MASTER_VOLUME = 0.5f;
    public const float MUSIC_VOLUME = 0.5f;
    public const float EFFECTS_VOLUME = 0.5f;
    public const float INTERFACE_VOLUME = 0.5f;
    public const bool AUTOSAVE = true;
    public const int AUTOSAVE_INTERVAL_MINUTES = 5;
    public const bool CONTROL_HINTS = true;
    public const bool CROSSHAIR = true;
    public const int NAME_TAG_DISTANCE = 32;
    public const int MOUSE_SENSITIVITY = 5;
    public const bool REMEMBER_SHAPE = true;
    public const bool REMEMBER_ORIENTATION = true;

    public const int FOV_MIN = 60;
    public const int FOV_MAX = 120;
    public const int SENSITIVITY_MIN = 1;
    public const int SENSITIVITY_MAX = 10;
    public const int NAME_TAG_MIN = 0;
    public const int NAME_TAG_MAX = 64;
    public const int NAME_TAG_STEP = 4;

    private static readonly int[] _frameLimits = [0, 30, 60, 75, 90, 120, 144, 165, 240];
    private static readonly float[] _viewDistances = [500f, 750f, 1000f, 1500f, 2000f, 2500f, 3000f, 4000f, 5000f];
    private static readonly int[] _autosaveIntervals = [1, 2, 5, 10, 15, 20, 30, 45, 60];
    private static readonly int[] _aoSamplesOptions = [4, 8, 16];
    private static readonly int[] _bloomPassesOptions = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
    private static readonly ShadowQuality[] _shadowQualities =
    [
        ShadowQuality.Off,
        ShadowQuality.Low,
        ShadowQuality.Medium,
        ShadowQuality.High,
        ShadowQuality.Ultra,
    ];

    /// <summary>Gets a frame limit list that includes the current value.</summary>
    public static IReadOnlyList<int> GetFrameLimits(int current)
    {
        return WithValue(_frameLimits, current);
    }

    /// <summary>Gets a view distance list that includes the current value.</summary>
    public static IReadOnlyList<float> GetViewDistances(float current)
    {
        return WithValue(_viewDistances, current);
    }

    /// <summary>Gets a shadow quality list that includes the current value.</summary>
    public static IReadOnlyList<ShadowQuality> GetShadowQualities(ShadowQuality current)
    {
        return WithValue(_shadowQualities, current);
    }

    /// <summary>Gets an autosave interval list that includes the current value.</summary>
    public static IReadOnlyList<int> GetAutosaveIntervals(int current)
    {
        return WithValue(_autosaveIntervals, current);
    }

    /// <summary>Gets an AO sample list that includes the current value.</summary>
    public static IReadOnlyList<int> GetAoSamples(int current)
    {
        return WithValue(_aoSamplesOptions, current);
    }

    /// <summary>Gets a bloom pass list that includes the current value.</summary>
    public static IReadOnlyList<int> GetBloomPasses(int current)
    {
        return WithValue(_bloomPassesOptions, current);
    }

    private static IReadOnlyList<T> WithValue<T>(T[] values, T current)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (EqualityComparer<T>.Default.Equals(values[i], current))
            {
                return values;
            }
        }

        var combined = new List<T>(values) { current };
        combined.Sort();
        return combined;
    }
}
