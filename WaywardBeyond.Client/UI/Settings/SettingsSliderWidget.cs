using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI.Settings;

/// <summary>Draws a settings slider and reports value changes.</summary>
internal static class SettingsSliderWidget
{
    /// <summary>Draws a slider row control.</summary>
    public static void SettingsSlider(
        this UIBuilder<Material> ui,
        string id,
        float value,
        float min,
        float max,
        float step,
        string display,
        bool enabled,
        Constraints constraints,
        Action<string, float>? onChanged = null
    )
    {
        ControlResult<float> result = Widgets.Slider(
            ui,
            id,
            constraints,
            value,
            min,
            max,
            step,
            _ => display,
            enabled
        );

        if (result.Changed)
        {
            onChanged?.Invoke(id, result.Value);
        }
    }
}
