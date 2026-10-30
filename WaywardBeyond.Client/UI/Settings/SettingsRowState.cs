using System.Collections.Generic;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>One settings row as markup needs it.</summary>
internal readonly record struct SettingsRowState(
    bool IsSection,
    bool IsChoice,
    bool IsStepper,
    bool IsSlider,
    bool IsReset,
    string Id,
    string Label,
    bool Enabled,
    string[] Options,
    int Index,
    string Value,
    bool CanDecrease,
    bool CanIncrease,
    float SliderValue,
    float Min,
    float Max,
    float Step,
    string Display
) {
    /// <summary>Creates a section header row.</summary>
    public static SettingsRowState Section(string label)
    {
        return new SettingsRowState(
            true, false, false, false, false,
            string.Empty, label, true,
            [], 0, string.Empty, false, false, 0f, 0f, 1f, 0f, string.Empty
        );
    }

    /// <summary>Creates a choice row.</summary>
    public static SettingsRowState Choice(
        string id,
        string label,
        bool enabled,
        IReadOnlyList<string> options,
        int index
    )
    {
        var labels = new string[options.Count];
        for (var i = 0; i < options.Count; i++)
        {
            labels[i] = options[i];
        }

        return new SettingsRowState(
            false, true, false, false, false,
            id, label, enabled,
            labels, index, string.Empty, false, false, 0f, 0f, 1f, 0f, string.Empty
        );
    }

    /// <summary>Creates a stepper row.</summary>
    public static SettingsRowState Stepper(
        string id,
        string label,
        bool enabled,
        string value,
        bool canDecrease,
        bool canIncrease
    )
    {
        return new SettingsRowState(
            false, false, true, false, false,
            id, label, enabled,
            [], 0, value, canDecrease, canIncrease, 0f, 0f, 1f, 0f, string.Empty
        );
    }

    /// <summary>Creates a slider row.</summary>
    public static SettingsRowState Slider(
        string id,
        string label,
        bool enabled,
        float value,
        float min,
        float max,
        float step,
        string display
    )
    {
        return new SettingsRowState(
            false, false, false, true, false,
            id, label, enabled,
            [], 0, string.Empty, false, false, value, min, max, step, display
        );
    }

    /// <summary>Creates a reset row.</summary>
    public static SettingsRowState Reset(string id)
    {
        return new SettingsRowState(
            false, false, false, false, true,
            id, string.Empty, true,
            [], 0, string.Empty, false, false, 0f, 0f, 1f, 0f, string.Empty
        );
    }
}
