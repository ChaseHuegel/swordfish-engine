using System;
using System.Collections.Generic;
using Reef.Constraints;
using Reef.UI;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>One settings row as markup needs it.</summary>
internal readonly record struct SettingsRowState(
    bool IsSection,
    bool IsChoice,
    bool IsStepper,
    bool IsSlider,
    bool IsReset,
    string Id,
    string LabelKey,
    bool Enabled,
    string[] OptionKeys,
    int Index,
    string Value,
    string ValueKey,
    bool CanDecrease,
    bool CanIncrease,
    float SliderValue,
    float Min,
    float Max,
    float Step,
    string Display
) {
    /// <summary>Gets the slider fill width for the current value.</summary>
    public IConstraint Fill => new Relative(Max > Min ? Math.Clamp((SliderValue - Min) / (Max - Min), 0f, 1f) : 0f);

    /// <summary>Creates a section header row.</summary>
    public static SettingsRowState Section(string labelKey)
    {
        return new SettingsRowState(
            true, false, false, false, false,
            string.Empty, labelKey, true,
            [], 0, string.Empty, string.Empty, false, false, 0f, 0f, 1f, 0f, string.Empty
        );
    }

    /// <summary>Creates a choice row.</summary>
    public static SettingsRowState Choice(
        string id,
        string labelKey,
        bool enabled,
        string[] optionKeys,
        int index
    )
    {
        return new SettingsRowState(
            false, true, false, false, false,
            id, labelKey, enabled,
            optionKeys, index, string.Empty, string.Empty, false, false, 0f, 0f, 1f, 0f, string.Empty
        );
    }

    /// <summary>Creates a stepper row.</summary>
    public static SettingsRowState Stepper(
        string id,
        string labelKey,
        bool enabled,
        string value,
        string valueKey,
        bool canDecrease,
        bool canIncrease
    )
    {
        return new SettingsRowState(
            false, false, true, false, false,
            id, labelKey, enabled,
            [], 0, value, valueKey, canDecrease, canIncrease, 0f, 0f, 1f, 0f, string.Empty
        );
    }

    /// <summary>Creates a slider row.</summary>
    public static SettingsRowState Slider(
        string id,
        string labelKey,
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
            id, labelKey, enabled,
            [], 0, string.Empty, string.Empty, false, false, value, min, max, step, display
        );
    }

    /// <summary>Creates a reset row.</summary>
    public static SettingsRowState Reset(string id)
    {
        return new SettingsRowState(
            false, false, false, false, true,
            id, string.Empty, true,
            [], 0, string.Empty, string.Empty, false, false, 0f, 0f, 1f, 0f, string.Empty
        );
    }
}
