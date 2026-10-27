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
        return ui.SegmentedControl(
            id,
            rect,
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
        T newValue = value;
        var changed = false;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.ControlBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                Color = enabled ? UiTheme.Border : UiTheme.BorderInner,
            };
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            IntRect localRect = Rect(0, 0, rect.Size.X, rect.Size.Y);
            IntRect inner = Inset(localRect, UiMetrics.PANEL_BORDER_THICKNESS);
            int count = options.Count;
            if (count == 0)
            {
                return ControlResult<T>.Unchanged(value);
            }

            int cellWidth = inner.Size.X / count;

            for (var i = 0; i < count; i++)
            {
                int left = inner.Left + i * cellWidth;
                int right = i == count - 1 ? inner.Right : left + cellWidth;
                var cellRect = new IntRect(left, inner.Top, right, inner.Bottom);
                bool selected = EqualityComparer<T>.Default.Equals(options[i], value);

                string label = optionLabel(options[i]);
                if (DrawCell(ui, $"{id}.{i}", in cellRect, label, selected, enabled))
                {
                    newValue = options[i];
                    changed = true;
                }
            }

            for (var i = 1; i < count; i++)
            {
                DrawCellDivider(ui, inner.Left + i * cellWidth, in inner);
            }
        }

        return new ControlResult<T>(changed, newValue);
    }

    /// <summary>Draws one segmented cell and reports a click on an unselected cell.</summary>
    private static bool DrawCell(
        UIBuilder<Material> ui,
        string id,
        in IntRect rect,
        string label,
        bool selected,
        bool enabled
    )
    {
        if (!enabled)
        {
            using (ui.Element())
            {
                DrawChoiceCell(ui, rect, label, selected, enabled: false, hovering: false);
            }

            return false;
        }

        using (ui.Element(id))
        {
            bool clicked = ui.Clicked();
            bool hovering = ui.Hovering();
            DrawChoiceCell(ui, rect, label, selected, enabled: true, hovering);
            return clicked && !selected;
        }
    }

    /// <summary>Draws the divider between two segmented cells.</summary>
    private static void DrawCellDivider(UIBuilder<Material> ui, int x, in IntRect inner)
    {
        using (ui.Element())
        {
            ui.Color = UiTheme.BorderInner;
            ui.Constraints = new Constraints
            {
                X = new Fixed(x),
                Y = new Fixed(inner.Top),
                Width = new Fixed(1),
                Height = new Fixed(inner.Size.Y),
            };
        }
    }

    private static void DrawChoiceCell(
        UIBuilder<Material> ui,
        IntRect rect,
        string label,
        bool selected,
        bool enabled,
        bool hovering
    )
    {
        ControlStyle style = UiThemeResolver.ResolveChoiceCell(selected, enabled, hovering);
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = style.Fill;
        ui.Constraints = RectConstraints(rect);
        ui.ClipConstraints = RectConstraints(rect);

        using (ui.Text(label))
        {
            ui.FontOptions = new FontOptions
            {
                ID = UiTheme.FONT_UI,
                Size = UiMetrics.CONTROL_FONT,
                NoWrap = true,
            };
            ui.Color = style.Text;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
            };
        }
    }
}
