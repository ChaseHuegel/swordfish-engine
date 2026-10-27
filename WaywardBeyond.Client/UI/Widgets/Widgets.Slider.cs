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
        float newValue = value;
        var changed = false;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Color = UiTheme.Transparent;
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            int buttonSize = UiMetrics.SLIDER_BUTTON_SIZE;
            int valueWidth = UiMetrics.SLIDER_VALUE_WIDTH;
            int gap = 8;
            int buttonY = (rect.Size.Y - buttonSize) / 2;

            IntRect minusRect = Rect(0, buttonY, buttonSize, buttonSize);
            IntRect valueRect = Rect(rect.Size.X - valueWidth, 0, valueWidth, rect.Size.Y);
            IntRect plusRect = Rect(valueRect.Left - gap - buttonSize, buttonY, buttonSize, buttonSize);
            int trackX = minusRect.Right + gap;
            int trackWidth = plusRect.Left - gap - trackX;
            IntRect trackRect = Rect(
                trackX,
                (rect.Size.Y - UiMetrics.SLIDER_TRACK_HEIGHT) / 2,
                trackWidth,
                UiMetrics.SLIDER_TRACK_HEIGHT
            );

            if (ui.ActionButton($"{id}.minus", minusRect, "-", ButtonVariant.Secondary, enabled))
            {
                newValue = SnapSliderValue(newValue - step, min, max, step);
                changed = true;
            }

            if (ui.ActionButton($"{id}.plus", plusRect, "+", ButtonVariant.Secondary, enabled))
            {
                newValue = SnapSliderValue(newValue + step, min, max, step);
                changed = true;
            }

            if (trackWidth > 10 && DrawSliderTrack(ui, enabled ? $"{id}.track" : null, trackRect, newValue, min, max, step, enabled, out float trackValue))
            {
                newValue = trackValue;
                changed = true;
            }

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Transparent;
                ui.Constraints = RectConstraints(valueRect);
                ui.ClipConstraints = RectConstraints(valueRect);

                using (ui.Text(format(newValue)))
                {
                    ui.FontOptions = new FontOptions
                    {
                        ID = UiTheme.FONT_UI,
                        Size = UiMetrics.CONTROL_FONT,
                        NoWrap = true,
                    };
                    ui.Color = enabled ? UiTheme.Text : UiTheme.TextDim;
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Right | Anchors.Center,
                    };
                }
            }
        }

        return new ControlResult<float>(changed, newValue);
    }

    private static bool DrawSliderTrack(
        UIBuilder<Material> ui,
        string? id,
        IntRect rect,
        float value,
        float min,
        float max,
        float step,
        bool enabled,
        out float newValue
    )
    {
        newValue = value;
        var changed = false;

        if (enabled)
        {
            using (ui.Element(id!))
            {
                changed = UpdateSliderFromCursor(ui, rect, min, max, step, ref newValue);
                DrawSliderTrackBody(ui, rect, newValue, min, max, enabled);
            }
        }
        else
        {
            using (ui.Element())
            {
                DrawSliderTrackBody(ui, rect, newValue, min, max, enabled);
            }
        }

        return changed;
    }

    private static bool UpdateSliderFromCursor(
        UIBuilder<Material> ui,
        IntRect rect,
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

        int innerWidth = Math.Max(1, rect.Size.X - 2);
        IntVector2 cursor = ui.GetRelativeCursorPosition();
        float ratio = Math.Clamp((cursor.X - 1f) / innerWidth, 0f, 1f);
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
        IntRect rect,
        float value,
        float min,
        float max,
        bool enabled
    )
    {
        ui.LayoutDirection = LayoutDirection.None;
        ui.Color = UiTheme.Track;
        ui.Border = new BorderStyle
        {
            Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
            Color = enabled ? UiTheme.Border : UiTheme.BorderInner,
        };
        ui.Constraints = RectConstraints(rect);
        ui.ClipConstraints = RectConstraints(rect);

        int innerWidth = Math.Max(1, rect.Size.X - 2);
        float ratio = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
        int fillWidth = (int)MathF.Round(ratio * innerWidth);

        using (ui.Element())
        {
            ui.Color = enabled ? UiTheme.TrackFill : UiTheme.BorderInner;
            ui.Constraints = new Constraints
            {
                X = new Fixed(1),
                Y = new Fixed(1),
                Width = new Fixed(fillWidth),
                Height = new Fixed(Math.Max(0, rect.Size.Y - 2)),
            };
        }

        for (var i = 1; i < 10; i++)
        {
            using (ui.Element())
            {
                ui.Color = UiTheme.Track * new Vector4(0.5f);
                ui.Constraints = new Constraints
                {
                    X = new Fixed(1 + innerWidth * i / 10),
                    Y = new Fixed(1),
                    Width = new Fixed(2),
                    Height = new Fixed(Math.Max(0, rect.Size.Y - 2)),
                };
            }
        }

        using (ui.Element())
        {
            int thumbX = 1 + Math.Clamp(fillWidth - 1, 0, Math.Max(0, innerWidth - 3));
            ui.Color = enabled ? UiTheme.Thumb : UiTheme.TextDim;
            ui.Constraints = new Constraints
            {
                X = new Fixed(thumbX),
                Y = new Fixed(1),
                Width = new Fixed(3),
                Height = new Fixed(Math.Max(0, rect.Size.Y - 2)),
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
