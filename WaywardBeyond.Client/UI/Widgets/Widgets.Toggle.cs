using System;
using System.Collections.Generic;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    private static readonly bool[] BOOL_OPTIONS = [false, true];

    /// <summary>Creates an on/off control and reports whether the value changed.</summary>
    public static ControlResult<bool> Toggle(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        bool value,
        string onLabel,
        string offLabel,
        bool enabled
    )
    {
        return ui.Toggle(id, RectConstraints(rect), value, onLabel, offLabel, enabled);
    }

    /// <summary>Creates an on/off control in the current layout and reports whether the value changed.</summary>
    public static ControlResult<bool> Toggle(
        this UIBuilder<Material> ui,
        string id,
        Constraints constraints,
        bool value,
        string onLabel,
        string offLabel,
        bool enabled
    )
    {
        return ui.SegmentedControl(
            id,
            constraints,
            BOOL_OPTIONS,
            value,
            option => option ? onLabel : offLabel,
            enabled
        );
    }

    /// <summary>Creates a row of radio-like choices and reports whether the value changed.</summary>
    public static ControlResult<T> SegmentedControl<T>(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        IReadOnlyList<T> options,
        T value,
        Func<T, string> optionLabel,
        bool enabled
    )
    {
        return ui.SegmentedControl(id, RectConstraints(rect), options, value, optionLabel, enabled);
    }

    /// <summary>Creates a row of radio-like choices in the current layout and reports whether the value changed.</summary>
    public static ControlResult<T> SegmentedControl<T>(
        this UIBuilder<Material> ui,
        string id,
        Constraints constraints,
        IReadOnlyList<T> options,
        T value,
        Func<T, string> optionLabel,
        bool enabled
    )
    {
        T newValue = value;
        var changed = false;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(UiMetrics.PANEL_BORDER_THICKNESS);
            ui.Color = UiTheme.ControlBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = enabled ? UiTheme.Border : UiTheme.BorderInner,
            };
            ui.Constraints = constraints;
            ui.ClipConstraints = ClipToBounds();

            int count = options.Count;
            if (count == 0)
            {
                return ControlResult<T>.Unchanged(value);
            }

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                },
                spacing: 1
            ))
            {
                ui.Color = UiTheme.BorderInner;

                for (var i = 0; i < count; i++)
                {
                    bool selected = EqualityComparer<T>.Default.Equals(options[i], value);
                    string label = optionLabel(options[i]);
                    if (DrawCell(ui, $"{id}.{i}", label, selected, enabled))
                    {
                        newValue = options[i];
                        changed = true;
                    }
                }
            }
        }

        return new ControlResult<T>(changed, newValue);
    }

    /// <summary>Draws one segmented cell and reports a click on an unselected cell.</summary>
    private static bool DrawCell(
        UIBuilder<Material> ui,
        string id,
        string label,
        bool selected,
        bool enabled
    )
    {
        if (!enabled)
        {
            using (ui.Element())
            {
                DrawChoiceCell(ui, label, selected, enabled: false, hovering: false);
            }

            return false;
        }

        using (ui.Element(id))
        {
            bool clicked = ui.Clicked();
            bool hovering = ui.Hovering();
            DrawChoiceCell(ui, label, selected, enabled: true, hovering);
            return clicked && !selected;
        }
    }

    private static void DrawChoiceCell(
        UIBuilder<Material> ui,
        string label,
        bool selected,
        bool enabled,
        bool hovering
    )
    {
        ControlStyle style = UiThemeResolver.ResolveChoiceCell(selected, enabled, hovering);
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = style.Fill;
        ui.Border = new BorderStyle
        {
            Thickness = style.BorderThickness,
            Color = style.Border,
        };
        ui.Constraints = new Constraints
        {
            Width = new Fill(),
            Height = new Fill(),
        };

        ui.Label(
            label,
            UiMetrics.CONTROL_FONT,
            style.Text,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            HorizontalAlignment.Center
        );
    }
}
