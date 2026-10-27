using System;
using System.Globalization;
using Swordfish.Library.Globalization;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Builds the controls settings category.</summary>
internal static class ControlSettingsCategory
{
    public static SettingsCategory Build(SettingsDraft draft, ILocalization localization, Action reset)
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
                    get: () => draft.MouseSensitivity,
                    set: value => draft.MouseSensitivity = (int)MathF.Round(value),
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
                    get: () => draft.RememberShape,
                    set: value => draft.RememberShape = value,
                    defaultValue: () => SettingsDefaults.REMEMBER_SHAPE
                ),
                new ToggleEntry(
                    id: "Settings.RememberOrientation",
                    labelKey: "ui.settings.rememberOrientation",
                    get: () => draft.RememberOrientation,
                    set: value => draft.RememberOrientation = value,
                    defaultValue: () => SettingsDefaults.REMEMBER_ORIENTATION
                ),
                new SettingsResetRow("Settings.Reset.controls", reset),
            ]
        );
    }

    private static string FormatInteger(float value)
    {
        return MathF.Round(value).ToString(CultureInfo.CurrentCulture);
    }

    private static string L(ILocalization localization, string key)
    {
        return localization.GetString(key) ?? key;
    }
}
