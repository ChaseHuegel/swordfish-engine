using System;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Builds the gameplay settings category.</summary>
internal static class GameplaySettingsCategory
{
    public static SettingsCategory Build(SettingsDraft draft, ILocalization localization, Action reset)
    {
        return new SettingsCategory(
            id: "gameplay",
            titleKey: "ui.settings.category.gameplay",
            rows:
            [
                new SettingsSection("ui.settings.section.autosave"),
                new ToggleEntry(
                    id: "Settings.Autosave",
                    labelKey: "ui.settings.option.autosave",
                    get: () => draft.Autosave,
                    set: value => draft.Autosave = value,
                    defaultValue: () => SettingsDefaults.AUTOSAVE
                ),
                new StepperEntry<int>(
                    id: "Settings.AutosaveInterval",
                    labelKey: "ui.settings.option.autosaveInterval",
                    get: () => draft.AutosaveIntervalMinutes,
                    set: value => draft.AutosaveIntervalMinutes = value,
                    defaultValue: () => SettingsDefaults.AUTOSAVE_INTERVAL_MINUTES,
                    options: SettingsDefaults.GetAutosaveIntervals(draft.AutosaveIntervalMinutes),
                    format: value => FormatMinutes(localization, value),
                    isEnabled: () => draft.Autosave
                ),
                new SettingsSection("ui.settings.section.hud"),
                new ToggleEntry(
                    id: "Settings.ControlHints",
                    labelKey: "ui.settings.option.controlHints",
                    get: () => draft.ControlHints,
                    set: value => draft.ControlHints = value,
                    defaultValue: () => SettingsDefaults.CONTROL_HINTS
                ),
                new ToggleEntry(
                    id: "Settings.Crosshair",
                    labelKey: "ui.settings.option.crosshair",
                    get: () => draft.Crosshair,
                    set: value => draft.Crosshair = value,
                    defaultValue: () => SettingsDefaults.CROSSHAIR
                ),
                new SliderEntry(
                    id: "Settings.NameTagDistance",
                    labelKey: "ui.settings.option.nameTagDistance",
                    get: () => draft.NameTagDistance,
                    set: value => draft.NameTagDistance = (int)MathF.Round(value),
                    defaultValue: () => SettingsDefaults.NAME_TAG_DISTANCE,
                    min: SettingsDefaults.NAME_TAG_MIN,
                    max: SettingsDefaults.NAME_TAG_MAX,
                    step: SettingsDefaults.NAME_TAG_STEP,
                    format: value => FormatNameTagDistance(localization, value)
                ),
                new SettingsResetRow("Settings.Reset.gameplay", reset),
            ]
        );
    }

    private static string FormatMinutes(ILocalization localization, int value)
    {
        string unit = L(localization, "ui.word.minutes");
        return $"{value} {unit}";
    }

    private static string FormatNameTagDistance(ILocalization localization, float value)
    {
        if (value <= 0f)
        {
            return L(localization, "ui.settings.value.off");
        }

        string unit = L(localization, "ui.unit.meters");
        return $"{MathF.Round(value)} {unit}";
    }

    private static string L(ILocalization localization, string key)
    {
        return localization.GetString(key) ?? key;
    }
}
