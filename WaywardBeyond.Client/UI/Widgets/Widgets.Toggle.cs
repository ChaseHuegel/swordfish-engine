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
    private static readonly bool[] BoolOptions = [false, true];

    /// <summary>Creates an on/off control and returns true when the value changes.</summary>
    public static bool Toggle(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        bool value,
        string onLabel,
        string offLabel,
        bool enabled,
        out bool newValue
    )
    {
        return ui.SegmentedControl(
            id,
            rect,
            BoolOptions,
            value,
            option => option ? onLabel : offLabel,
            enabled,
            out newValue
        );
    }

    /// <summary>Creates a row of radio-like choices and returns true when the value changes.</summary>
    public static bool SegmentedControl<T>(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        IReadOnlyList<T> options,
        T value,
        Func<T, string> optionLabel,
        bool enabled,
        out T newValue
    )
    {
        newValue = value;
        var changed = false;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.ControlBackground;
            ui.Border = new BorderStyle
            {
                Thickness = UiMetrics.PanelBorderThickness,
                Color = enabled ? UiTheme.Border : UiTheme.BorderInner,
            };
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            IntRect localRect = Rect(0, 0, rect.Size.X, rect.Size.Y);
            IntRect inner = Inset(localRect, UiMetrics.PanelBorderThickness);
            int count = options.Count;
            if (count == 0)
            {
                return false;
            }

            int cellWidth = inner.Size.X / count;

            for (var i = 0; i < count; i++)
            {
                int left = inner.Left + i * cellWidth;
                int right = i == count - 1 ? inner.Right : left + cellWidth;
                var cellRect = new IntRect(left, inner.Top, right, inner.Bottom);
                bool selected = EqualityComparer<T>.Default.Equals(options[i], value);
                string cellId = $"{id}.{i}";

                if (enabled)
                {
                    using (ui.Element(cellId))
                    {
                        bool clicked = ui.Clicked();
                        bool hovering = ui.Hovering();
                        DrawChoiceCell(ui, cellRect, optionLabel(options[i]), selected, enabled: true, hovering);

                        if (clicked && !selected)
                        {
                            newValue = options[i];
                            changed = true;
                        }
                    }
                }
                else
                {
                    using (ui.Element())
                    {
                        DrawChoiceCell(ui, cellRect, optionLabel(options[i]), selected, enabled: false, hovering: false);
                    }
                }
            }

            for (var i = 1; i < count; i++)
            {
                using (ui.Element())
                {
                    int x = inner.Left + i * cellWidth;
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
        }

        return changed;
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
                ID = UiTheme.FontUI,
                Size = UiMetrics.ControlFont,
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
