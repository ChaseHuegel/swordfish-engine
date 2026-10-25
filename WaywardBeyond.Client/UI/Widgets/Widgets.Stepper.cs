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
    /// <summary>Creates a discrete stepper and returns true when the selected index changes.</summary>
    public static bool Stepper<T>(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        IReadOnlyList<T> options,
        int index,
        Func<T, string> format,
        bool enabled,
        out int newIndex
    )
    {
        newIndex = Math.Clamp(index, 0, Math.Max(0, options.Count - 1));
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
            int arrowWidth = Math.Min(UiMetrics.StepperArrowWidth, Math.Max(1, inner.Size.X / 3));
            var previousRect = new IntRect(inner.Left, inner.Top, inner.Left + arrowWidth, inner.Bottom);
            var nextRect = new IntRect(inner.Right - arrowWidth, inner.Top, inner.Right, inner.Bottom);
            var valueRect = new IntRect(previousRect.Right, inner.Top, nextRect.Left, inner.Bottom);

            bool canPrevious = enabled && newIndex > 0;
            bool canNext = enabled && newIndex < options.Count - 1;

            if (canPrevious)
            {
                using (ui.Element($"{id}.previous"))
                {
                    bool clicked = ui.Clicked();
                    bool hovering = ui.Hovering();
                    DrawStepperArrow(ui, previousRect, "<", enabled: true, hovering: hovering);
                    if (clicked)
                    {
                        newIndex--;
                        changed = true;
                    }
                }
            }
            else
            {
                using (ui.Element())
                {
                    DrawStepperArrow(ui, previousRect, "<", enabled: false, hovering: false);
                }
            }

            if (canNext)
            {
                using (ui.Element($"{id}.next"))
                {
                    bool clicked = ui.Clicked();
                    bool hovering = ui.Hovering();
                    DrawStepperArrow(ui, nextRect, ">", enabled: true, hovering: hovering);
                    if (clicked)
                    {
                        newIndex++;
                        changed = true;
                    }
                }
            }
            else
            {
                using (ui.Element())
                {
                    DrawStepperArrow(ui, nextRect, ">", enabled: false, hovering: false);
                }
            }

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Transparent;
                ui.Constraints = RectConstraints(valueRect);
                ui.ClipConstraints = RectConstraints(valueRect);

                using (ui.Text(options.Count > 0 ? format(options[newIndex]) : string.Empty))
                {
                    ui.FontOptions = new FontOptions
                    {
                        ID = UiTheme.FontUI,
                        Size = UiMetrics.ControlFont,
                        NoWrap = true,
                    };
                    ui.Color = enabled ? UiTheme.Text : UiTheme.TextDim;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Center,
                    };
                }
            }

            DrawStepperSeparator(ui, inner, previousRect.Right);
            DrawStepperSeparator(ui, inner, nextRect.Left);
        }

        return changed;
    }

    private static void DrawStepperArrow(
        UIBuilder<Material> ui,
        IntRect rect,
        string symbol,
        bool enabled,
        bool hovering
    )
    {
        ControlStyle style = UiThemeResolver.ResolveStepperArrow(enabled, hovering);
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = style.Fill;
        ui.Constraints = RectConstraints(rect);
        ui.ClipConstraints = RectConstraints(rect);

        using (ui.Text(symbol))
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

    private static void DrawStepperSeparator(UIBuilder<Material> ui, IntRect inner, int x)
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
}
