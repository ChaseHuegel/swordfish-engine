using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using WaywardBeyond.Client.UI.Controls;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    private static readonly Regex _wordRegex = WordRegex();

    /// <summary>
    ///     Searches for words, ignoring whitespace.
    ///     Example: "Hello world!"
    ///     Groups: "Hello", "world!"
    /// </summary>
    [GeneratedRegex(@"\S+")]
    private static partial Regex WordRegex();

    /// <summary>
    ///     Creates a text box that can be typed into.
    /// </summary>
    /// <returns>True if the text box is being interacted with; otherwise false.</returns>
    public static bool TextBox<T>(
        this UIBuilder<T> ui,
        string id,
        ref TextBoxState state,
        FontOptions fontOptions,
        IInputService inputService,
        out Interactions interactions,
        Style? style = null,
        TextInputPalette? palette = null,
        Constraints? constraintsOverride = null,
        string? placeholderOverride = null
    )
    {
        fontOptions.ID ??= ui.DefaultFontID;

        using (ui.Element(id))
        {
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(left: 0, top: 0, right: 0, bottom: 0);

            Constraints? constraints = constraintsOverride ?? state.Settings.Constraints;
            if (constraints != null)
            {
                ui.Constraints = constraints.Value;
                ui.ClipConstraints = new Constraints
                {
                    Width = new Relative(1f),
                    Height = new Relative(1f),
                };
            }

            bool clicked = ui.Clicked();
            bool held = ui.Held();
            bool hovering = ui.Hovering();
            bool entered = ui.Entered();
            bool exited = ui.Exited();
            bool focused = ui.Focused();

            if (focused != state.Focused)
            {
                state.ControlModifier = false;
                state.ShiftModifier = false;
                state.SelectionStartIndex = state.CaretIndex;
            }

            Vector4 textColor = style?.Color ?? (focused
                ? palette?.TextFocused ?? UiTheme.Text
                : palette?.Text ?? UiTheme.TextMuted);
            Vector4 placeholderColor = focused
                ? palette?.PlaceholderFocused ?? UiTheme.TextMuted
                : palette?.Placeholder ?? UiTheme.TextDim;
            Vector4 selectionColor = palette?.Selection ?? UiTheme.RowSelected;
            Vector4 caretColor = palette?.Caret ?? UiTheme.Text;

            if (style != null)
            {
                ui.Color = style.Value.BackgroundColor;
                ui.BackgroundColor = style.Value.BackgroundColor;
                ui.Border = style.Value.Border;
                ui.CornerRadius = style.Value.CornerRadius;
                ui.Padding = style.Value.Padding;
            }
            else if (state.Settings.Chrome)
            {
                if (focused)
                {
                    ui.Color = UiTheme.InputFocus;
                    ui.Border = new BorderStyle
                    {
                        Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                        Color = UiTheme.BorderHover,
                    };
                }
                else
                {
                    ui.Color = UiTheme.InputBackground;
                    ui.Border = new BorderStyle
                    {
                        Thickness = UiMetrics.PANEL_BORDER_THICKNESS,
                        Color = UiTheme.Border,
                    };
                }
            }
            else
            {
                ui.Color = UiTheme.Transparent;
                ui.Border = default;
            }

            var submitted = false;
            var typing = false;
            var navigating = false;
            var editing = false;
            var selectionOverwritten = false;

            TextLayout textLayout = ui.GetTextLayout(id + "_Text");

            if (clicked || held)
            {
                IntVector2 relativeCursorPosition = ui.GetRelativeCursorPosition();
                selectionOverwritten = true;

                //  Check if the cursor is outside the text bounds vertically
                if (relativeCursorPosition.Y < 0)
                {
                    state.CaretIndex = 0;
                }
                else if (relativeCursorPosition.Y > textLayout.Constraints.PreferredHeight && textLayout.Lines.Length > 0)
                {
                    state.CaretIndex = state.Text.Length;
                }
                else
                {
                    state.CaretIndex = FindCaretIndexAt(textLayout, state.Text, relativeCursorPosition);
                }

                if (!held)
                {
                    state.SelectionStartIndex = state.CaretIndex;
                }
            }

            if (focused)
            {
                IReadOnlyCollection<UIController.Input> inputBuffer = ui.GetInputBuffer();
                foreach (UIController.Input input in inputBuffer)
                {
                    bool currentlyHasSelection = state.HasSelection();

                    if (input.Type == UIController.InputType.Char && !state.ControlModifier)
                    {
                        if (state.Settings.DisallowedCharacters != null && state.Settings.DisallowedCharacters.Contains(input.Char))
                        {
                            continue;
                        }

                        if (currentlyHasSelection)
                        {
                            TextBoxState.Selection currentSelection = state.CalculateSelection();
                            state.Text.Remove(currentSelection.StartIndex, currentSelection.Length);
                            state.CaretIndex = currentSelection.StartIndex;
                        }

                        state.Text.Insert(state.CaretIndex, input.Char);
                        state.CaretIndex += 1;
                        typing = true;
                    }
                    else if (input.Type != UIController.InputType.KeyPress)
                    {
                        if (input.Key == UIController.Key.Control)
                        {
                            state.ControlModifier = false;
                        }
                        else if (input.Key == UIController.Key.Shift)
                        {
                            state.ShiftModifier = false;
                        }

                        continue;
                    }
                    else if (input == UIController.Key.Control)
                    {
                        state.ControlModifier = true;
                    }
                    else if (input == UIController.Key.Shift)
                    {
                        state.ShiftModifier = true;
                    }
                    else if (input == UIController.Key.Backspace && state.CaretIndex <= state.Text.Length && (currentlyHasSelection || state.CaretIndex > 0))
                    {
                        DeleteBackward(ref state, currentlyHasSelection);
                        typing = true;
                    }
                    else if (input == UIController.Key.Delete && (currentlyHasSelection || state.CaretIndex <= state.Text.Length - 1))
                    {
                        DeleteForward(ref state, currentlyHasSelection);
                        typing = true;
                    }
                    else if (input == UIController.Key.Tab)
                    {
                        if (state.Settings.DisallowedCharacters != null && state.Settings.DisallowedCharacters.Contains('\t'))
                        {
                            continue;
                        }

                        state.Text.Insert(state.CaretIndex, '\t');
                        state.CaretIndex += 1;
                        typing = true;
                    }
                    else if (input == UIController.Key.LeftArrow)
                    {
                        if (state.ControlModifier)
                        {
                            var textStr = state.Text.ToString();
                            Match previousWord = _wordRegex.MatchPrevious(textStr, state.CaretIndex);
                            state.CaretIndex = previousWord.Success ? previousWord.Index : 0;
                        }
                        else
                        {
                            state.CaretIndex -= 1;
                        }

                        navigating = true;
                    }
                    else if (input == UIController.Key.RightArrow)
                    {
                        if (state.ControlModifier)
                        {
                            var textStr = state.Text.ToString();
                            Match nextWord = _wordRegex.MatchNext(textStr, state.CaretIndex);
                            state.CaretIndex = nextWord.Success ? nextWord.Index : state.Text.Length;
                        }
                        else
                        {
                            state.CaretIndex += 1;
                        }

                        navigating = true;
                    }
                    else if (input == UIController.Key.UpArrow || input == UIController.Key.DownArrow)
                    {
                        if (input == UIController.Key.UpArrow && state.CaretIndex <= 0 || input == UIController.Key.DownArrow && state.CaretIndex >= state.Text.Length)
                        {
                            continue;
                        }

                        //  Get info about the current line
                        int currentLineIndex = -1;
                        var currentLineTextStart = 0;
                        var currentLineGlyphStart = 0;
                        var textIndex = 0;
                        var glyphIndex = 0;

                        for (var i = 0; i < textLayout.Lines.Length; i++)
                        {
                            string line = textLayout.Lines[i];

                            bool isHardBreak = textIndex + line.Length < state.Text.Length && state.Text[textIndex + line.Length] == '\n';
                            int effectiveLineTextEnd = textIndex + line.Length + (isHardBreak ? 1 : 0);

                            if (state.CaretIndex >= textIndex && state.CaretIndex < effectiveLineTextEnd || i == textLayout.Lines.Length - 1 && state.CaretIndex == effectiveLineTextEnd)
                            {
                                currentLineIndex = i;
                                currentLineTextStart = textIndex;
                                currentLineGlyphStart = glyphIndex;
                                break;
                            }

                            textIndex = effectiveLineTextEnd;
                            glyphIndex += line.Length;
                        }

                        if (currentLineIndex == -1)
                        {
                            continue;
                        }

                        //  Calculate visual X offset
                        var visualOffsetX = 0;
                        int caretIndexOnLine = state.CaretIndex - currentLineTextStart;
                        int glyphsOnCurrentLine = textLayout.Lines[currentLineIndex].Length;
                        if (caretIndexOnLine > 0 && glyphsOnCurrentLine > 0)
                        {
                            int caretGlyphIndex = currentLineGlyphStart + Math.Min(caretIndexOnLine, glyphsOnCurrentLine) - 1;
                            visualOffsetX = textLayout.Glyphs[caretGlyphIndex].BBOX.Right;
                        }

                        //  Find target line
                        int targetLineIndex = input == UIController.Key.UpArrow ? currentLineIndex - 1 : currentLineIndex + 1;

                        //  If already on the first line and navigating up,
                        //  jump to the first character.
                        if (targetLineIndex < 0)
                        {
                            state.CaretIndex = 0;
                        }
                        //  If already on the last line and navigating down,
                        //  jump to the last character.
                        else if (targetLineIndex >= textLayout.Lines.Length)
                        {
                            state.CaretIndex = state.Text.Length;
                        }
                        else
                        {
                            //  Get target line info by re-calculating indices
                            textIndex = 0;
                            glyphIndex = 0;
                            for (var i = 0; i < targetLineIndex; i++)
                            {
                                string line = textLayout.Lines[i];
                                bool isHardBreak = textIndex + line.Length < state.Text.Length && state.Text[textIndex + line.Length] == '\n';

                                textIndex += line.Length + (isHardBreak ? 1 : 0);
                                glyphIndex += line.Length;
                            }

                            int targetLineTextStart = textIndex;
                            int targetLineGlyphStart = glyphIndex;
                            int targetLineLength = textLayout.Lines[targetLineIndex].Length;

                            state.CaretIndex = FindClosestCaretIndexInLine(textLayout, targetLineTextStart, targetLineGlyphStart, targetLineLength, visualOffsetX);
                        }

                        navigating = true;
                    }
                    else if (input == UIController.Key.Home)
                    {
                        state.CaretIndex = 0;
                        navigating = true;
                    }
                    else if (input == UIController.Key.End)
                    {
                        state.CaretIndex = state.Text.Length;
                        navigating = true;
                    }
                    else if (input == UIController.Key.C && state.ControlModifier)
                    {
                        if (!currentlyHasSelection)
                        {
                            state.SelectionStartIndex = 0;
                            state.CaretIndex = state.Text.Length;
                            selectionOverwritten = true;
                        }

                        TextBoxState.Selection currentSelection = state.CalculateSelection();
                        var textStr = state.Text.ToString(currentSelection.StartIndex, currentSelection.Length);
                        inputService.SetClipboard(textStr);
                    }
                    else if (input == UIController.Key.X && state.ControlModifier)
                    {
                        if (!currentlyHasSelection)
                        {
                            state.SelectionStartIndex = 0;
                            state.CaretIndex = state.Text.Length;
                            selectionOverwritten = true;
                        }

                        TextBoxState.Selection currentSelection = state.CalculateSelection();

                        var textStr = state.Text.ToString(currentSelection.StartIndex, currentSelection.Length);
                        inputService.SetClipboard(textStr);

                        state.Text.Remove(currentSelection.StartIndex, currentSelection.Length);
                        state.CaretIndex = currentSelection.StartIndex;
                        editing = true;
                    }
                    else if (input == UIController.Key.V && state.ControlModifier)
                    {
                        string clipboardContent = inputService.GetClipboard();

                        if (state.Settings.DisallowedCharacters != null)
                        {
                            var hasDisallowedCharacter = false;
                            for (var i = 0; i < clipboardContent.Length; i++)
                            {
                                if (!state.Settings.DisallowedCharacters.Contains(clipboardContent[i]))
                                {
                                    continue;
                                }

                                hasDisallowedCharacter = true;
                                break;
                            }

                            if (hasDisallowedCharacter)
                            {
                                continue;
                            }
                        }

                        if (currentlyHasSelection)
                        {
                            TextBoxState.Selection currentSelection = state.CalculateSelection();
                            state.Text.Remove(currentSelection.StartIndex, currentSelection.Length);
                            state.CaretIndex = currentSelection.StartIndex;
                        }

                        state.Text.Insert(state.CaretIndex, clipboardContent);
                        state.CaretIndex += clipboardContent.Length;
                        editing = true;
                    }
                    else if (input == UIController.Key.A && state.ControlModifier)
                    {
                        state.SelectionStartIndex = 0;
                        state.CaretIndex = state.Text.Length;
                        selectionOverwritten = true;
                    }
                    else if (input == UIController.Key.Enter)
                    {
                        if (state.Settings.SubmitBehavior == TextBoxState.SubmitBehavior.Submit)
                        {
                            submitted = true;
                            ui.Unfocus();
                        }
                        else
                        {
                            if (currentlyHasSelection)
                            {
                                TextBoxState.Selection currentSelection = state.CalculateSelection();
                                state.Text.Remove(currentSelection.StartIndex, currentSelection.Length);
                                state.CaretIndex = currentSelection.StartIndex;
                            }

                            state.Text.Insert(state.CaretIndex, '\n');
                            state.CaretIndex += 1;
                            typing = true;
                        }
                    }

                    if (state.Settings.MaxCharacters > 0 && state.Text.Length > state.Settings.MaxCharacters.Value)
                    {
                        int truncateStartIndex = state.Settings.MaxCharacters.Value;
                        state.Text.Remove(truncateStartIndex, state.Text.Length - state.Settings.MaxCharacters.Value);
                    }

                    editing = typing || editing;
                    state.CaretIndex = Math.Clamp(state.CaretIndex, 0, state.Text.Length);

                    if (!selectionOverwritten && (editing || (navigating && !state.ShiftModifier)))
                    {
                        state.SelectionStartIndex = state.CaretIndex;
                    }
                }

                if (editing || navigating)
                {
                    state.LastInputTime = ui.Time;
                }
            }
            else
            {
                state.SelectionStartIndex = state.CaretIndex;
            }

            bool isPlaceholder = state.Text.Length == 0;
            var textContent = state.Text.ToString();

            TextBoxState.Selection selection = state.CalculateSelection();

            bool hasSelection = state.HasSelection();
            int selectionStart = selection.StartIndex;
            int lineStride = textLayout.Constraints.PreferredHeight / Math.Max(textLayout.Lines.Length, 1);

            //  Selection paints behind the glyphs.
            DrawSelection(ui, state, textLayout, selection, hasSelection, lineStride, selectionColor);

            if (isPlaceholder)
            {
                using (ui.Text(placeholderOverride ?? state.Settings.Placeholder ?? ""))
                {
                    ui.Passthrough = true;
                    ui.FontOptions = fontOptions;
                    ui.Color = placeholderColor;
                }
            }

            using (ui.Text(textContent))
            {
                ui.ID = id + "_Text";
                ui.Passthrough = true;
                ui.FontOptions = fontOptions;

                ui.Color = textColor;
            }

            //  The caret paints over the glyphs.
            DrawCaret(ui, state, textLayout, selectionStart, hasSelection, focused, lineStride, caretColor);

            interactions = Interactions.None;

            if (clicked)
            {
                interactions |= Interactions.Click;
            }

            if (held)
            {
                interactions |= Interactions.Held;
            }

            if (hovering)
            {
                interactions |= Interactions.Hover;
            }

            if (entered)
            {
                interactions |= Interactions.Enter;
            }

            if (exited)
            {
                interactions |= Interactions.Exit;
            }

            if (typing)
            {
                interactions |= Interactions.Input;
            }

            if (submitted)
            {
                interactions |= Interactions.Submit;
            }

            state.Changed = typing;
            state.Submitted = submitted;
            state.Focused = focused;
            state.Hovered = hovering;
            state.Entered = entered;
            state.Exited = exited;

            return interactions != Interactions.None;
        }
    }

    /// <summary>Finds the caret index nearest to a cursor position inside the text layout.</summary>
    private static int FindCaretIndexAt(TextLayout textLayout, StringBuilder text, IntVector2 cursorPosition)
    {
        var textIndex = 0;
        var glyphIndex = 0;

        for (var lineIndex = 0; lineIndex < textLayout.Lines.Length; lineIndex++)
        {
            string line = textLayout.Lines[lineIndex];
            int lineTop = lineIndex * textLayout.LineHeight;
            bool onLine = cursorPosition.Y >= lineTop && cursorPosition.Y <= lineTop + textLayout.LineHeight;

            if (onLine)
            {
                return FindCaretIndexOnLine(textLayout, line, textIndex, glyphIndex, cursorPosition.X);
            }

            bool isHardBreak = textIndex + line.Length < text.Length && text[textIndex + line.Length] == '\n';
            textIndex += line.Length + (isHardBreak ? 1 : 0);
            glyphIndex += line.Length;
        }

        return text.Length;
    }

    /// <summary>Finds the caret index nearest to a horizontal position on one line.</summary>
    private static int FindCaretIndexOnLine(
        TextLayout textLayout,
        string line,
        int textIndex,
        int glyphIndex,
        float cursorX
    )
    {
        if (line.Length == 0)
        {
            return textIndex;
        }

        for (var i = 0; i < line.Length; i++)
        {
            GlyphLayout glyph = textLayout.Glyphs[glyphIndex + i];
            int midPoint = glyph.BBOX.Left + glyph.BBOX.Size.X / 2;

            if (cursorX < midPoint)
            {
                return textIndex + i;
            }
        }

        return textIndex + line.Length;
    }

    /// <summary>Deletes the selection, the previous word, or one character behind the caret.</summary>
    private static void DeleteBackward(ref TextBoxState state, bool hasSelection)
    {
        int deleteStartIndex = state.CaretIndex - 1;
        var countToDelete = 1;

        if (hasSelection)
        {
            TextBoxState.Selection selection = state.CalculateSelection();
            deleteStartIndex = selection.StartIndex;
            countToDelete = selection.Length;
            state.CaretIndex = deleteStartIndex;
        }
        else if (state.ControlModifier)
        {
            string text = state.Text.ToString();
            Match previousWord = _wordRegex.MatchPrevious(text, state.CaretIndex);
            deleteStartIndex = previousWord.Success ? previousWord.Index : 0;
            countToDelete = state.CaretIndex - deleteStartIndex;
            state.CaretIndex = deleteStartIndex;
        }
        else
        {
            state.CaretIndex -= countToDelete;
        }

        state.Text.Remove(deleteStartIndex, countToDelete);
    }

    /// <summary>Deletes the selection, the next word, or one character ahead of the caret.</summary>
    private static void DeleteForward(ref TextBoxState state, bool hasSelection)
    {
        int deleteStartIndex = state.CaretIndex;
        var countToDelete = 1;

        if (hasSelection)
        {
            TextBoxState.Selection selection = state.CalculateSelection();
            deleteStartIndex = selection.StartIndex;
            countToDelete = selection.Length;
            state.CaretIndex = deleteStartIndex;
        }
        else if (state.ControlModifier)
        {
            string text = state.Text.ToString();
            Match nextWord = _wordRegex.MatchNext(text, state.CaretIndex);
            countToDelete = nextWord.Success
                ? nextWord.Index - state.CaretIndex
                : state.Text.Length - state.CaretIndex;
        }

        state.Text.Remove(deleteStartIndex, countToDelete);
    }

    private static void DrawSelection<T>(
        UIBuilder<T> ui,
        TextBoxState state,
        TextLayout textLayout,
        TextBoxState.Selection selection,
        bool hasSelection,
        int lineStride,
        Vector4 selectionColor
    )
    {
        if (!hasSelection)
        {
            return;
        }

        int selectionStart = selection.StartIndex;
        int selectionEnd = selectionStart + selection.Length;

        var layoutTextIndex = 0;
        var layoutGlyphIndex = 0;

        for (var i = 0; i < textLayout.Lines.Length; i++)
        {
            string line = textLayout.Lines[i];
            int lineGlyphStart = layoutGlyphIndex;
            int lineTextStart = layoutTextIndex;
            int lineTextEnd = layoutTextIndex + line.Length;

            bool isHardBreak = lineTextEnd < state.Text.Length && state.Text[lineTextEnd] == '\n';

            int selStartOnLine = Math.Max(selectionStart, lineTextStart);
            int selEndOnLine = Math.Min(selectionEnd, lineTextEnd);

            if (selStartOnLine < selEndOnLine)
            {
                int startGlyphIndexOnLine = selStartOnLine - lineTextStart;
                int endGlyphIndexOnLine = selEndOnLine - lineTextStart;

                GlyphLayout startGlyph = textLayout.Glyphs[lineGlyphStart + startGlyphIndexOnLine];
                GlyphLayout endGlyph = textLayout.Glyphs[lineGlyphStart + endGlyphIndexOnLine - 1];

                int x = startGlyph.BBOX.Left;
                int width = endGlyph.BBOX.Right - x;

                using (ui.Element())
                {
                    ui.Color = selectionColor;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(x),
                        Y = new Fixed(i * lineStride),
                        Width = new Fixed(width),
                        Height = new Fixed(lineStride),
                    };
                }
            }

            layoutTextIndex = lineTextEnd + (isHardBreak ? 1 : 0);
            layoutGlyphIndex = lineGlyphStart + line.Length;
        }
    }

    private static void DrawCaret<T>(
        UIBuilder<T> ui,
        TextBoxState state,
        TextLayout textLayout,
        int caretIndex,
        bool hasSelection,
        bool focused,
        int lineStride,
        Vector4 caretColor
    )
    {
        if (hasSelection || !focused)
        {
            return;
        }

        var layoutTextIndex = 0;
        var layoutGlyphIndex = 0;

        for (var i = 0; i < textLayout.Lines.Length; i++)
        {
            string line = textLayout.Lines[i];
            int lineGlyphStart = layoutGlyphIndex;
            int lineGlyphEnd = layoutGlyphIndex + line.Length;
            int lineTextStart = layoutTextIndex;
            int lineTextEnd = layoutTextIndex + line.Length;

            bool isHardBreak = lineTextEnd < state.Text.Length && state.Text[lineTextEnd] == '\n';
            int effectiveLineTextEnd = isHardBreak ? lineTextEnd + 1 : lineTextEnd;

            bool isCaretOnLine = caretIndex >= lineTextStart && caretIndex < effectiveLineTextEnd
                || (i == textLayout.Lines.Length - 1 && caretIndex == effectiveLineTextEnd);

            if (isCaretOnLine && (ui.Time % 1f < 0.5f || ui.Time - state.LastInputTime < 0.5f))
            {
                int x;

                if (caretIndex >= lineTextEnd)
                {
                    x = line.Length == 0
                        ? 0
                        : textLayout.Glyphs[lineGlyphEnd - 1].BBOX.Right;
                }
                else
                {
                    int caretGlyphIndexOnLine = caretIndex - lineTextStart;
                    x = textLayout.Glyphs[lineGlyphStart + caretGlyphIndexOnLine].BBOX.Left;
                }

                using (ui.Element())
                {
                    ui.Color = caretColor;
                    ui.Constraints = new Constraints
                    {
                        X = new Fixed(x + 1),
                        Y = new Fixed(i * lineStride),
                        Width = new Fixed(1),
                        Height = new Fixed(lineStride),
                    };
                }
            }

            layoutTextIndex = effectiveLineTextEnd;
            layoutGlyphIndex = lineGlyphEnd;
        }
    }

    private static int FindClosestCaretIndexInLine(TextLayout layout, int lineTextStart, int lineGlyphStart, int lineLength, int targetX)
    {
        if (lineLength == 0)
        {
            return lineTextStart;
        }

        // Default to start of the line
        int bestIndex = lineTextStart;
        int minDistance = Math.Abs(layout.Glyphs[lineGlyphStart].BBOX.Left - targetX);

        for (var i = 0; i < lineLength; i++)
        {
            int x = layout.Glyphs[lineGlyphStart + i].BBOX.Right;
            int dist = Math.Abs(x - targetX);

            if (dist < minDistance)
            {
                minDistance = dist;
                bestIndex = lineTextStart + i + 1;
            }
        }

        return bestIndex;
    }
}
