using System;
using System.Collections.Generic;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    private static readonly Vector4 CHOICE_BACKGROUND = FromRgb(21, 29, 42);
    private static readonly Vector4 CHOICE_BORDER = FromRgb(43, 56, 78);
    private static readonly Vector4 CHOICE_BORDER_INNER = FromRgb(26, 35, 51);
    private static readonly Vector4 CHOICE_HOVER = FromRgb(27, 37, 56);
    private static readonly Vector4 CHOICE_SELECTED = FromRgb(22, 40, 56);
    private static readonly Vector4 CHOICE_SELECTED_TEXT = FromRgb(165, 243, 252);
    private static readonly Vector4 CHOICE_TEXT = FromRgb(240, 243, 246);
    private static readonly Vector4 CHOICE_TEXT_MUTED = FromRgb(141, 160, 184);
    private static readonly Vector4 CHOICE_TEXT_DIM = FromRgb(69, 84, 107);

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
            ui.Padding = new Padding(1);
            ui.Color = CHOICE_BACKGROUND;
            ui.Border = new BorderStyle
            {
                Thickness = 1,
                Color = enabled ? CHOICE_BORDER : CHOICE_BORDER_INNER,
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
                ui.Color = CHOICE_BORDER_INNER;

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
        Vector4 fill;
        Vector4 text;

        if (!enabled)
        {
            fill = CHOICE_BACKGROUND;
            text = CHOICE_TEXT_DIM;
        }
        else if (selected)
        {
            fill = CHOICE_SELECTED;
            text = CHOICE_SELECTED_TEXT;
        }
        else if (hovering)
        {
            fill = CHOICE_HOVER;
            text = CHOICE_TEXT;
        }
        else
        {
            fill = CHOICE_BACKGROUND;
            text = CHOICE_TEXT_MUTED;
        }

        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = fill;
        ui.Border = new BorderStyle
        {
            Thickness = 0,
            Color = Vector4.Zero,
        };
        ui.Constraints = new Constraints
        {
            Width = new Fill(),
            Height = new Fill(),
        };

        ui.Label(
            label,
            18,
            text,
            new Constraints
            {
                Width = new Fill(),
                Height = new Fill(),
            },
            HorizontalAlignment.Center
        );
    }
}
