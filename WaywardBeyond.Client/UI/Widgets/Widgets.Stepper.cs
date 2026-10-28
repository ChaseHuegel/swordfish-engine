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
    /// <summary>Creates a discrete stepper and reports whether the selected index changed.</summary>
    public static ControlResult<int> Stepper<T>(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        IReadOnlyList<T> options,
        int index,
        Func<T, string> format,
        bool enabled
    )
    {
        return ui.Stepper(id, RectConstraints(rect), options, index, format, enabled);
    }

    /// <summary>Creates a discrete stepper in the current layout and reports whether the selected index changed.</summary>
    public static ControlResult<int> Stepper<T>(
        this UIBuilder<Material> ui,
        string id,
        Constraints constraints,
        IReadOnlyList<T> options,
        int index,
        Func<T, string> format,
        bool enabled
    )
    {
        int newIndex = Math.Clamp(index, 0, Math.Max(0, options.Count - 1));
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

            using (ui.Row(
                new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                }
            ))
            {
                ui.Color = UiTheme.ControlBackground;

                bool canPrevious = enabled && newIndex > 0;
                if (canPrevious)
                {
                    using (ui.Element($"{id}.previous"))
                    {
                        bool clicked = ui.Clicked();
                        bool hovering = ui.Hovering();
                        DrawStepperArrow(ui, "<", enabled: true, hovering);
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
                        DrawStepperArrow(ui, "<", enabled: false, hovering: false);
                    }
                }

                DrawStepperSeparator(ui);

                ui.Label(
                    options.Count > 0 ? format(options[newIndex]) : string.Empty,
                    UiMetrics.CONTROL_FONT,
                    enabled ? UiTheme.Text : UiTheme.TextDim,
                    new Constraints
                    {
                        Width = new Fill(),
                        Height = new Fill(),
                    },
                    HorizontalAlignment.Center
                );

                DrawStepperSeparator(ui);

                bool canNext = enabled && newIndex < options.Count - 1;
                if (canNext)
                {
                    using (ui.Element($"{id}.next"))
                    {
                        bool clicked = ui.Clicked();
                        bool hovering = ui.Hovering();
                        DrawStepperArrow(ui, ">", enabled: true, hovering);
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
                        DrawStepperArrow(ui, ">", enabled: false, hovering: false);
                    }
                }
            }
        }

        return new ControlResult<int>(changed, newIndex);
    }


    private static void DrawStepperSeparator(UIBuilder<Material> ui)
    {
        using (ui.Element())
        {
            ui.Color = UiTheme.BorderInner;
            ui.Constraints = new Constraints
            {
                Width = new Fixed(1),
                Height = new Fill(),
            };
        }
    }

    private static void DrawStepperArrow(
        UIBuilder<Material> ui,
        string symbol,
        bool enabled,
        bool hovering
    )
    {
        ControlStyle style = UiThemeResolver.ResolveStepperArrow(enabled, hovering);
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = style.Fill;
        ui.Border = new BorderStyle
        {
            Thickness = style.BorderThickness,
            Color = style.Border,
        };
        ui.Constraints = new Constraints
        {
            Width = new Fixed(UiMetrics.STEPPER_ARROW_WIDTH),
            Height = new Fill(),
        };

        ui.Label(
            symbol,
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
