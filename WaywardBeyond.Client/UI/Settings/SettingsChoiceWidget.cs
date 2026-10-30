using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Draws a settings choice control and reports selection changes.</summary>
internal static class SettingsChoiceWidget
{
    /// <summary>Draws a radio-like choice control.</summary>
    public static void SettingsChoice(
        this UIBuilder<Material> ui,
        string id,
        string[] options,
        int value,
        bool enabled,
        Constraints constraints,
        Action<string, int>? onChanged = null
    )
    {
        var indices = new int[options.Length];
        for (var i = 0; i < options.Length; i++)
        {
            indices[i] = i;
        }

        ControlResult<int> result = Widgets.SegmentedControl<int>(
            ui,
            id,
            constraints,
            indices,
            value,
            index => options[index],
            enabled
        );

        if (result.Changed)
        {
            onChanged?.Invoke(id, result.Value);
        }
    }
}
