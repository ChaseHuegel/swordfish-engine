using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Builds the audio settings category.</summary>
internal static class AudioSettingsCategory
{
    public static SettingsCategory Build(SettingsDraft draft, ILocalization localization, Action reset)
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
                    () => draft.MasterVolume,
                    value => draft.MasterVolume = value,
                    SettingsDefaults.MASTER_VOLUME,
                    localization
                ),
                BuildVolumeSlider(
                    "Settings.MusicVolume",
                    "ui.settings.musicVolume",
                    () => draft.MusicVolume,
                    value => draft.MusicVolume = value,
                    SettingsDefaults.MUSIC_VOLUME,
                    localization
                ),
                BuildVolumeSlider(
                    "Settings.EffectsVolume",
                    "ui.settings.effectsVolume",
                    () => draft.EffectsVolume,
                    value => draft.EffectsVolume = value,
                    SettingsDefaults.EFFECTS_VOLUME,
                    localization
                ),
                BuildVolumeSlider(
                    "Settings.InterfaceVolume",
                    "ui.settings.interfaceVolume",
                    () => draft.InterfaceVolume,
                    value => draft.InterfaceVolume = value,
                    SettingsDefaults.INTERFACE_VOLUME,
                    localization
                ),
                new SettingsResetRow("Settings.Reset.audio", reset),
            ]
        );
    }

    private static SliderEntry BuildVolumeSlider(
        string id,
        string labelKey,
        Func<float> get,
        Action<float> set,
        float defaultVolume,
        ILocalization localization
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

    private static string FormatPercent(float value)
    {
        return $"{MathF.Round(value * 100f)}%";
    }

    private static string L(ILocalization localization, string key)
    {
        return localization.GetString(key) ?? key;
    }
}
