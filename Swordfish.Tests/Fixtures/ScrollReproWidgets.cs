using System;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Components;

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
        new SegmentedControl().Render(
            ui,
            parameters: new SegmentedControlParameters(id, options, value, enabled, onChanged),
            callerConstraints: new ConstraintsOverride
            {
                Width = constraints.Width,
                Height = constraints.Height,
                X = constraints.X,
                Y = constraints.Y,
                Anchors = constraints.Anchors == 0 ? null : constraints.Anchors,
            }
        );
    }
}
