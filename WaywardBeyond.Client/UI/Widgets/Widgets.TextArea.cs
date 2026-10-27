using System;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    private const int TEXT_AREA_GUTTER = 10;
    private const int TEXT_AREA_SCROLLBAR_WIDTH = 3;
    private const int TEXT_AREA_SCROLLBAR_INSET = 4;
    private const int TEXT_AREA_WHEEL_LINES = 3;

    /// <summary>Creates a framed multi-line text box that scrolls when its text overflows the rect.</summary>
    /// <returns>True if the text area was submitted; otherwise false.</returns>
    public static bool TextArea(
        this UIBuilder<Material> ui,
        string id,
        IntRect rect,
        ref TextBoxState state,
        FontOptions fontOptions,
        IInputService inputService,
        SoundEffectService soundEffectService
    )
    {
        int viewportWidth = Math.Max(0, rect.Size.X - TEXT_AREA_GUTTER);
        int viewportHeight = rect.Size.Y;

        //  Text layouts commit at Build, so this reads the previous frame's wrapped height.
        TextLayout layout = ui.GetTextLayout(id + "_Text");
        int contentHeight = layout.Constraints.PreferredHeight;
        int lineStride = layout.Constraints.PreferredHeight / Math.Max(layout.Lines?.Length ?? 0, 1);
        if (lineStride <= 0)
        {
            lineStride = fontOptions.Size;
        }

        int maxScroll = Math.Max(0, contentHeight - viewportHeight);
        Interactions interactions;
        bool submitted;

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = RectConstraints(rect);
            ui.ClipConstraints = RectConstraints(rect);

            bool hoveringFrame = ui.Hovering(id + "_Frame");

            using (ui.Element())
            {
                ui.LayoutDirection = LayoutDirection.None;
                ui.Color = UiTheme.Transparent;
                ui.ScrollY = -state.ScrollY;
                ui.Constraints = RectConstraints(Rect(0, 0, viewportWidth, viewportHeight));
                ui.ClipConstraints = new Constraints
                {
                    Width = new Fixed(viewportWidth),
                    Height = new Fixed(viewportHeight),
                };

                //  The text box fills at least the viewport so clicks below the text still focus it.
                var textBoxConstraints = new Constraints
                {
                    Width = new Fixed(viewportWidth),
                    Height = new Fixed(Math.Max(viewportHeight, contentHeight)),
                };

                ui.TextBox(id, ref state, fontOptions, inputService, out interactions, drawChrome: false, constraints: textBoxConstraints);
                submitted = interactions.WithTextInputAudio(soundEffectService);
            }

            //  Children are closed, so these styles apply to the frame with this frame's focus state.
            if (state.Focused)
            {
                ui.Color = UiTheme.InputFocus;
                ui.Border = new BorderStyle
                {
                    Thickness = UiMetrics.PanelBorderThickness,
                    Color = UiTheme.BorderHover,
                };
            }
            else
            {
                ui.Color = UiTheme.InputBackground;
                ui.Border = new BorderStyle
                {
                    Thickness = UiMetrics.PanelBorderThickness,
                    Color = UiTheme.Border,
                };
            }

            float wheel = inputService.GetMouseScroll();
            bool hovering = hoveringFrame || interactions.Has(Interactions.Hover);
            if (hovering && wheel != 0f)
            {
                state.ScrollY -= (int)MathF.Round(wheel * lineStride * TEXT_AREA_WHEEL_LINES);
            }
            else if (state.Focused && state.LastInputTime >= ui.Time - ui.Delta)
            {
                //  Edits and caret moves keep the caret line inside the viewport. The check also covers
                //  the frame after an edit, because the wrapped layout lags one frame behind the text.
                int caretTop = GetCaretLine(state, layout) * lineStride;
                if (caretTop < state.ScrollY)
                {
                    state.ScrollY = caretTop;
                }
                else if (caretTop + lineStride > state.ScrollY + viewportHeight)
                {
                    state.ScrollY = caretTop + lineStride - viewportHeight;
                }
            }

            state.ScrollY = Math.Clamp(state.ScrollY, 0, maxScroll);

            IntRect trackRect = Rect(
                rect.Size.X - TEXT_AREA_SCROLLBAR_INSET - TEXT_AREA_SCROLLBAR_WIDTH,
                TEXT_AREA_SCROLLBAR_INSET,
                TEXT_AREA_SCROLLBAR_WIDTH,
                Math.Max(0, viewportHeight - TEXT_AREA_SCROLLBAR_INSET * 2)
            );
            ui.Scrollbar(trackRect, contentHeight, viewportHeight, state.ScrollY);
        }

        return submitted;
    }

    /// <summary>Gets the wrapped line index that holds the caret.</summary>
    private static int GetCaretLine(in TextBoxState state, in TextLayout layout)
    {
        if (layout.Lines == null || layout.Lines.Length == 0)
        {
            return 0;
        }

        var textIndex = 0;
        for (var i = 0; i < layout.Lines.Length; i++)
        {
            int lineEnd = textIndex + layout.Lines[i].Length;
            bool isHardBreak = lineEnd < state.Text.Length && state.Text[lineEnd] == '\n';
            int effectiveLineEnd = lineEnd;
            if (isHardBreak)
            {
                effectiveLineEnd = lineEnd + 1;
            }

            if (state.CaretIndex < effectiveLineEnd)
            {
                return i;
            }

            textIndex = effectiveLineEnd;
        }

        return layout.Lines.Length - 1;
    }
}
