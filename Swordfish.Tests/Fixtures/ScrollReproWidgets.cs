using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI;

namespace Swordfish.Tests;

/// <summary>Markup widgets used by the scroll input repro.</summary>
internal static class ScrollReproWidgets
{
    /// <summary>Draws a choice control and reports selection changes.</summary>
    public static void Choice(
        this UIBuilder<Material> ui,
        string id,
        string[] options,
        int value,
        bool enabled,
        Constraints constraints,
        Action<string, int>? onChanged = null
    )
    {
        ControlResult<string> result = Widgets.SegmentedControl<string>(
            ui,
            id,
            constraints,
            options,
            options[value],
            option => option,
            enabled
        );

        if (result.Changed)
        {
            onChanged?.Invoke(id, System.Array.IndexOf(options, result.Value));
        }
    }
}
