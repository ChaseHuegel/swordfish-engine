using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Creates a draggable slider and reports whether the value changed.</summary>
    public static ControlResult<float> Slider(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        float value,
        float min,
        float max,
        float step,
        Func<float, string> format,
        bool enabled
    )
    {
        return ui.Slider(id, RectConstraints(rect), value, min, max, step, format, enabled);
    }

    /// <summary>Creates a draggable slider in the current layout and reports whether the value changed.</summary>
    public static ControlResult<float> Slider(
        this UIBuilder<Material> ui,
        string id,
        Constraints constraints,
        float value,
        float min,
        float max,
        float step,
        Func<float, string> format,
        bool enabled
    )
    {
        float newValue = value;
        var changed = false;

        using (ui.Row(constraints, spacing: 8))
        {
            var buttonOptions = new ActionButtonOptions(
                Variant: ButtonVariant.Secondary,
                Layout: new Constraints
                {
                    Width = new Fixed(UiMetrics.SLIDER_BUTTON_SIZE),
                    Height = new Fixed(UiMetrics.SLIDER_BUTTON_SIZE),
                },
                Anchors: Anchors.Center
            );

            if (ui.ActionButton($"{id}.minus", "-", buttonOptions with { Enabled = enabled }))
            {
                newValue = SnapSliderValue(newValue - step, min, max, step);
                changed = true;
            }

            int trackWidth = 0;
            if (enabled)
            {
                using (ui.Element($"{id}.track"))
                {
                    ElementBounds bounds = ui.GetElementBounds();
                    trackWidth = bounds.Rect.Size.X;
                    changed = UpdateSliderFromCursor(ui, bounds, min, max, step, ref newValue);
                    DrawSliderTrackBody(ui, trackWidth, newValue, min, max, enabled);
                }
            }
            else
            {
                using (ui.Element())
                {
                    DrawSliderTrackBody(ui, trackWidth, newValue, min, max, enabled);
                }
            }

            if (ui.ActionButton($"{id}.plus", "+", buttonOptions with { Enabled = enabled }))
            {
                newValue = SnapSliderValue(newValue + step, min, max, step);
                changed = true;
            }

            ui.Label(
                format(newValue),
                UiMetrics.CONTROL_FONT,
                enabled ? UiTheme.Text : UiTheme.TextDim,
                new Constraints
                {
                    Width = new Fixed(UiMetrics.SLIDER_VALUE_WIDTH),
                    Height = new Fill(),
                },
                HorizontalAlignment.Right
            );
        }

        return new ControlResult<float>(changed, newValue);
    }

    private static bool UpdateSliderFromCursor(
        UIBuilder<Material> ui,
        ElementBounds bounds,
        float min,
        float max,
        float step,
        ref float value
    )
    {
        if (!ui.Clicked() && !ui.Held())
        {
            return false;
        }

        int trackWidth = bounds.Rect.Size.X;
        if (trackWidth <= 2)
        {
            return false;
        }

        IntVector2 cursor = ui.GetRelativeCursorPosition();
        float ratio = Math.Clamp((cursor.X - 1f) / (trackWidth - 2), 0f, 1f);
        float snapped = SnapSliderValue(min + ratio * (max - min), min, max, step);

        if (Math.Abs(snapped - value) < 0.0001f)
        {
            return false;
        }

        value = snapped;
        return true;
    }

    private static void DrawSliderTrackBody(
        UIBuilder<Material> ui,
        int trackWidth,
        float value,
        float min,
        float max,
        bool enabled
    )
    {
        ui.LayoutDirection = LayoutDirection.None;
        ui.Padding = new Padding(UiMetrics.PANEL_BORDER_THICKNESS);
        ui.Color = UiTheme.Track;
        ui.Border = new BorderStyle
        {
            Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
            Color = enabled ? UiTheme.Border : UiTheme.BorderInner,
        };
        ui.Constraints = new Constraints
        {
            Width = new Fill(),
            Height = new Fixed(UiMetrics.SLIDER_TRACK_HEIGHT),
            Anchors = Anchors.Center,
        };
        ui.ClipConstraints = ClipToBounds();

        float ratio = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;

        using (ui.Element())
        {
            ui.Color = enabled ? UiTheme.TrackFill : UiTheme.BorderInner;
            ui.Constraints = new Constraints
            {
                Width = new Relative(ratio),
                Height = new Fill(),
            };
        }

        for (var i = 1; i < 10; i++)
        {
            using (ui.Element())
            {
                ui.Color = UiTheme.Track * new Vector4(0.5f);
                ui.Constraints = new Constraints
                {
                    X = new Relative(i / 10f),
                    Width = new Fixed(2),
                    Height = new Fill(),
                };
            }
        }

        float thumbRatio = ratio;
        if (trackWidth > 4)
        {
            thumbRatio = Math.Min(thumbRatio, (trackWidth - 4f) / (trackWidth - 2f));
        }

        using (ui.Element())
        {
            ui.Color = enabled ? UiTheme.Thumb : UiTheme.TextDim;
            ui.Constraints = new Constraints
            {
                X = new Relative(thumbRatio),
                Width = new Fixed(3),
                Height = new Fill(),
            };
        }
    }

    private static float SnapSliderValue(float value, float min, float max, float step)
    {
        if (step <= 0f)
        {
            return Math.Clamp(value, min, max);
        }

        float snapped = MathF.Round((value - min) / step) * step + min;
        return Math.Clamp(snapped, min, max);
    }
}
