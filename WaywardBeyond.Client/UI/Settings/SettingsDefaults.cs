using System.Collections.Generic;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Recommended defaults for the settings page.</summary>
internal static class SettingsDefaults
{
    public const SettingsDisplayMode DisplayMode = SettingsDisplayMode.BorderlessWindow;
    public const bool VSync = false;
    public const int FrameLimit = 0;
    public const int Fov = 90;
    public const float ViewDistance = 1000f;
    public const AntiAliasing AntiAliasingMode = Swordfish.Settings.AntiAliasing.MSAA;
    public const bool AmbientOcclusion = true;
    public const int AoSamples = 8;
    public const bool Bloom = true;
    public const int BloomPasses = 10;
    public const float MasterVolume = 0.5f;
    public const float MusicVolume = 0.5f;
    public const float EffectsVolume = 0.5f;
    public const float InterfaceVolume = 0.5f;
    public const bool Autosave = true;
    public const int AutosaveIntervalMinutes = 5;
    public const bool ControlHints = true;
    public const bool Crosshair = true;
    public const int NameTagDistance = 32;
    public const int MouseSensitivity = 5;
    public const bool RememberShape = true;
    public const bool RememberOrientation = true;

    public const int FovMin = 60;
    public const int FovMax = 120;
    public const int SensitivityMin = 1;
    public const int SensitivityMax = 10;
    public const int NameTagMin = 0;
    public const int NameTagMax = 64;
    public const int NameTagStep = 4;

    private static readonly int[] FrameLimits = [0, 30, 60, 75, 90, 120, 144, 165, 240];
    private static readonly float[] ViewDistances = [500f, 750f, 1000f, 1500f, 2000f, 2500f, 3000f, 4000f, 5000f];
    private static readonly int[] AutosaveIntervals = [1, 2, 5, 10, 15, 20, 30, 45, 60];
    private static readonly int[] AoSamplesOptions = [4, 8, 16];
    private static readonly int[] BloomPassesOptions = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    /// <summary>Gets a frame limit list that includes the current value.</summary>
    public static IReadOnlyList<int> GetFrameLimits(int current)
    {
        return WithValue(FrameLimits, current);
    }

    /// <summary>Gets a view distance list that includes the current value.</summary>
    public static IReadOnlyList<float> GetViewDistances(float current)
    {
        return WithValue(ViewDistances, current);
    }

    /// <summary>Gets an autosave interval list that includes the current value.</summary>
    public static IReadOnlyList<int> GetAutosaveIntervals(int current)
    {
        return WithValue(AutosaveIntervals, current);
    }

    /// <summary>Gets an AO sample list that includes the current value.</summary>
    public static IReadOnlyList<int> GetAoSamples(int current)
    {
        return WithValue(AoSamplesOptions, current);
    }

    /// <summary>Gets a bloom pass list that includes the current value.</summary>
    public static IReadOnlyList<int> GetBloomPasses(int current)
    {
        return WithValue(BloomPassesOptions, current);
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
