using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using DryIoc.ImTools;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Library.Extensions;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

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
    /// <returns>True if the text box was submitted; otherwise false.</returns>
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, ref TextBoxState state, FontOptions fontOptions, IInputService inputService)
    {
        return TextBox(ui, id, ref state, fontOptions, inputService, out Interactions interactions) && (interactions & Interactions.Submit) == Interactions.Submit;
    }
    
    /// <summary>
    ///     Creates a text box that can be typed into, with audio cues.
    /// </summary>
    /// <returns>True if the text box was submitted; otherwise false.</returns>
    public static bool TextBox<T>(this UIBuilder<T> ui, string id, ref TextBoxState state, FontOptions fontOptions, IInputService inputService, IAudioService audioService, VolumeSettings volumeSettings)
    {
        return TextBox(ui, id, ref state, fontOptions, inputService, out Interactions interactions) && interactions.WithTextInputAudio(audioService, volumeSettings);
    }

    /// <summary>
    ///     Creates a text box that can be typed into.
    /// </summary>
    /// <returns>True if the text box is being interacted with; otherwise false.</returns>
    public static bool TextBox<T>(this UIBuilder<T> ui, string id, ref TextBoxState state, FontOptions fontOptions, IInputService inputService, out Interactions interactions)
    {
        using (ui.Element(id))
        {
            ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 0.1f);
            ui.LayoutDirection = LayoutDirection.None;
            ui.Padding = new Padding(left: 0, top: 0, right: 0, bottom: 0);
            
            if (state.Settings.Constraints != null)
            {
                ui.Constraints = state.Settings.Constraints.Value;
                ui.ClipConstraints = state.Settings.Constraints.Value;
            }

            bool clicked = ui.Clicked();
            bool held = ui.Held();
            bool hovering = ui.Hovering();
            bool entered = ui.Entered();
            bool exited = ui.Exited();
            bool focused = ui.Focused();
            
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
                
                // Check if the cursor is outside the text bounds vertically
                if (relativeCursorPosition.Y < 0)
                {
                    state.CaretIndex = 0;
                }
                else if (relativeCursorPosition.Y > textLayout.Constraints.PreferredHeight)
                {
                    state.CaretIndex = state.Text.Length;
                }
                
                var glyphIndex = 0;
                for (var lineIndex = 0; lineIndex < textLayout.Lines.Length; lineIndex++)
                {
                    int lineTop = lineIndex * textLayout.LineHeight;
                    
                    // Check if cursor is within the vertical bounds of this line
                    if (relativeCursorPosition.Y < lineTop || relativeCursorPosition.Y > lineTop + textLayout.LineHeight)
                    {
                        glyphIndex += textLayout.Lines[lineIndex].Length;
                        continue;
                    }
                    
                    // Check if the cursor is outside the left bound of this line
                    if (relativeCursorPosition.X < 0)
                    {
                        state.CaretIndex = glyphIndex;
                        glyphIndex += textLayout.Lines[lineIndex].Length;
                        continue;
                    }
                    
                    //  If the line is empty, place the caret at the start of it and continue
                    if (textLayout.Lines[lineIndex].Length == 0)
                    {
                        state.CaretIndex = glyphIndex;
                        continue;
                    }
                    
                    // Check if the cursor is outside the right bound of this line
                    int lineEndIndex = glyphIndex + textLayout.Lines[lineIndex].Length;
                    GlyphLayout lastGlyphInLine = textLayout.Glyphs[lineEndIndex - 1];
                    if (relativeCursorPosition.X > lastGlyphInLine.BBOX.Right)
                    {
                        //  If the glyph is a newline, the caret should be placed on it.
                        //  Otherwise, the caret doesn't visually appear on the line the click happened.
                        bool isNewline = lineEndIndex > 0 && lineEndIndex <= state.Text.Length && state.Text[lineEndIndex - 1] == '\n';
                        state.CaretIndex = isNewline ? lineEndIndex - 1 : lineEndIndex;
                        glyphIndex += textLayout.Lines[lineIndex].Length;
                        continue;
                    }

                    //  Check if the cursor is within the horizontal bounds of any glyphs on this line
                    for (var i = 0; i < textLayout.Lines[lineIndex].Length; i++)
                    {
                        GlyphLayout glyph = textLayout.Glyphs[glyphIndex];
                        
                        //  The hit area for a glyph is offset by half width,
                        //  so clicking between characters feels more natural.
                        int halfWidth = glyph.BBOX.Size.X / 2;

                        if (relativeCursorPosition.X > glyph.BBOX.Left - halfWidth)
                        {
                            state.CaretIndex = glyphIndex;
                        }

                        if (relativeCursorPosition.X > glyph.BBOX.Right - halfWidth)
                        {
                            //  If the glyph is a newline, the caret should be placed on it.
                            //  Otherwise, the caret doesn't visually appear on the line the click happened.
                            bool isNewLine = glyphIndex < state.Text.Length && state.Text[glyphIndex] == '\n';
                            state.CaretIndex = isNewLine ? glyphIndex : glyphIndex + 1;
                        }

                        glyphIndex++;
                    }
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
                    bool hasSelection = state.HasSelection();
                    
                    if (input.Type == UIController.InputType.Char && !state.ControlModifier)
                    {
                        if (state.Settings.DisallowedCharacters != null && state.Settings.DisallowedCharacters.Contains(input.Char))
                        {
                            continue;
                        }
                        
                        if (hasSelection)
                        {
                            TextBoxState.Selection selection = state.CalculateSelection();
                            state.Text.Remove(selection.StartIndex, selection.Length);
                            state.CaretIndex = selection.StartIndex;
                        }
                        
                        state.Text.Insert(state.CaretIndex, input.Char);
                        state.CaretIndex += 1;
                        typing = true;
                    }
                    else if (input.Type != UIController.InputType.KeyPress)
                    {
                        state.ControlModifier = state.ControlModifier && input != UIController.Key.Control;
                        state.ShiftModifier = state.ShiftModifier && input != UIController.Key.Shift;
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
                    else if (input == UIController.Key.Backspace && state.CaretIndex <= state.Text.Length && (hasSelection || state.CaretIndex > 0))
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
                            var textStr = state.Text.ToString();
                            Match previousWord = _wordRegex.MatchPrevious(textStr, state.CaretIndex);
                            
                            if (previousWord.Success)
                            {
                                deleteStartIndex = previousWord.Index;
                                countToDelete = state.CaretIndex - previousWord.Index;
                            }
                            else
                            {
                                deleteStartIndex = 0;
                                countToDelete = state.CaretIndex;
                            }
                            
                            state.CaretIndex -= countToDelete;
                        }
                        else
                        {
                            state.CaretIndex -= countToDelete;
                        }
                        
                        state.Text.Remove(deleteStartIndex, countToDelete);
                        typing = true;
                    }
                    else if (input == UIController.Key.Delete && (hasSelection || state.CaretIndex <= state.Text.Length - 1))
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
                            var textStr = state.Text.ToString();
                            Match nextWord = _wordRegex.MatchNext(textStr, state.CaretIndex);
                            
                            if (nextWord.Success)
                            {
                                countToDelete = nextWord.Index - state.CaretIndex;
                            }
                            else
                            {
                                countToDelete = state.Text.Length - state.CaretIndex;
                            }
                        }
                        
                        state.Text.Remove(deleteStartIndex, countToDelete);
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
                    else if (input == UIController.Key.UpArrow)
                    {
                        if (state.CaretIndex <= 0)
                        {
                            continue;
                        }
                        
                        int lineIndex = textLayout.Lines.Length - 1;
                        var lineStartIndex = 0;
                        var currentIndex = 0;
                        for (var i = 0; i < textLayout.Lines.Length; i++)
                        {
                            int lineLength = textLayout.Lines[i].Length;
                            if (state.CaretIndex < currentIndex + lineLength || (state.CaretIndex == currentIndex + lineLength && state.CaretIndex == state.Text.Length))
                            {
                                lineIndex = i;
                                lineStartIndex = currentIndex;
                                break;
                            }
                            currentIndex += lineLength;
                        }
                        
                        if (lineIndex <= 0)
                        {
                            continue;
                        }
                        
                        var visualOffsetX = 0;
                        if (state.CaretIndex > lineStartIndex)
                        {
                            visualOffsetX = textLayout.Glyphs[state.CaretIndex - 1].BBOX.Right;
                        }
                        
                        int prevLineStart = lineStartIndex - textLayout.Lines[lineIndex - 1].Length;
                        int prevLineEnd = lineStartIndex;
                        
                        state.CaretIndex = FindClosestCaretIndexInLine(textLayout, prevLineStart, prevLineEnd, visualOffsetX);
                        navigating = true;
                    }
                    else if (input == UIController.Key.DownArrow)
                    {
                        int lineIndex = textLayout.Lines.Length - 1;
                        var lineStartIndex = 0;
                        var currentIndex = 0;
                        for (var i = 0; i < textLayout.Lines.Length; i++)
                        {
                            int lineLength = textLayout.Lines[i].Length;
                            if (state.CaretIndex < currentIndex + lineLength || (state.CaretIndex == currentIndex + lineLength && state.CaretIndex == state.Text.Length))
                            {
                                lineIndex = i;
                                lineStartIndex = currentIndex;
                                break;
                            }
                            currentIndex += lineLength;
                        }

                        if (lineIndex >= textLayout.Lines.Length - 1)
                        {
                            continue;
                        }
                        
                        var visualOffsetX = 0;
                        if (state.CaretIndex > lineStartIndex)
                        {
                            visualOffsetX = textLayout.Glyphs[state.CaretIndex - 1].BBOX.Right;
                        }

                        int nextLineStart = lineStartIndex + textLayout.Lines[lineIndex].Length;
                        int nextLineEnd = nextLineStart + textLayout.Lines[lineIndex + 1].Length;

                        state.CaretIndex = FindClosestCaretIndexInLine(textLayout, nextLineStart, nextLineEnd, visualOffsetX);
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
                        if (!hasSelection)
                        {
                            state.SelectionStartIndex = 0;
                            state.CaretIndex = state.Text.Length;
                            selectionOverwritten = true;
                        }
                        
                        TextBoxState.Selection selection = state.CalculateSelection();
                        var textStr = state.Text.ToString(selection.StartIndex, selection.Length);
                        inputService.SetClipboard(textStr);
                    }
                    else if (input == UIController.Key.X && state.ControlModifier)
                    {
                        if (!hasSelection)
                        {
                            state.SelectionStartIndex = 0;
                            state.CaretIndex = state.Text.Length;
                            selectionOverwritten = true;
                        }
                        
                        TextBoxState.Selection selection = state.CalculateSelection();
                        
                        var textStr = state.Text.ToString(selection.StartIndex, selection.Length);
                        inputService.SetClipboard(textStr);
                        
                        state.Text.Remove(selection.StartIndex, selection.Length);
                        state.CaretIndex = selection.StartIndex;
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
                        
                        if (hasSelection)
                        {
                            TextBoxState.Selection selection = state.CalculateSelection();
                            state.Text.Remove(selection.StartIndex, selection.Length);
                            state.CaretIndex = selection.StartIndex;
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
                            if (hasSelection)
                            {
                                TextBoxState.Selection selection = state.CalculateSelection();
                                state.Text.Remove(selection.StartIndex, selection.Length);
                                state.CaretIndex = selection.StartIndex;
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
            
            TextLayout caretLayout = ui.TextEngine.Layout(fontOptions, state.CaretIndex > 0 ? textContent : "\0", 0, state.CaretIndex > 0 ? state.CaretIndex : 1, textLayout.Constraints.PreferredWidth);
            GlyphLayout caretGlyph = caretLayout.Glyphs.Length > 0 ? caretLayout.Glyphs[^1] : default;
            int lineStride = caretLayout.Constraints.PreferredHeight / caretLayout.Lines.Length;

            //  Render the caret if in focus and this isn't a blink frame
            if (focused && (ui.Time % 1f < 0.5f || ui.Time - state.LastInputTime < 0.5f))
            {
                using (ui.Element())
                {
                    ui.Color = new Vector4(1f, 1f, 1f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Bottom | Anchors.Left | Anchors.Local,
                        X = new Fixed(caretGlyph.BBOX.Right),
                        Y = new Fixed(caretLayout.Constraints.PreferredHeight - 2),
                        Width = new Fixed(2),
                        Height = new Fixed(lineStride),
                    };
                }
            }

            if (isPlaceholder)
            {
                using (ui.Text(state.Settings.Placeholder ?? ""))
                {
                    ui.Passthrough = true;
                    ui.FontOptions = fontOptions;
                    ui.Color = focused ? new Vector4(0.5f, 0.5f, 0.5f, 1f) : new Vector4(0.325f, 0.325f, 0.325f, 1f);
                }
            }

            using (ui.Text(textContent))
            {
                ui.ID = id + "_Text";
                ui.Passthrough = true;
                ui.FontOptions = fontOptions;

                if (focused)
                {
                    ui.Color = new Vector4(1f, 1f, 1f, 1f);
                }
                else
                {
                    ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                }
            }
            
            //  Render text selection
            if (state.HasSelection())
            {
                TextBoxState.Selection selection = state.CalculateSelection();

                int selectionStart = selection.StartIndex;
                int selectionEnd = selection.StartIndex + selection.Length;

                var glyphIndex = 0;
                for (var i = 0; i < textLayout.Lines.Length; i++)
                {
                    string line = textLayout.Lines[i];
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }

                    int lineEndGlyphIndex = glyphIndex + line.Length;

                    int lineSelectionStart = Math.Max(selectionStart, glyphIndex);
                    int lineSelectionEnd = Math.Min(selectionEnd, lineEndGlyphIndex);

                    if (lineSelectionStart < lineSelectionEnd)
                    {
                        GlyphLayout startGlyph = textLayout.Glyphs[lineSelectionStart];
                        GlyphLayout endGlyph = textLayout.Glyphs[lineSelectionEnd - 1];

                        int x = startGlyph.BBOX.Left;
                        int width = endGlyph.BBOX.Right - x;

                        //  Render the current line
                        using (ui.Element())
                        {
                            ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
                            ui.Constraints = new Constraints
                            {
                                X = new Fixed(x),
                                Y = new Fixed(i * lineStride),
                                Width = new Fixed(width),
                                Height = new Fixed(lineStride),
                            };
                        }
                    }

                    glyphIndex = lineEndGlyphIndex;
                }
            }

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

            return interactions != Interactions.None;
        }
    }
    
    private static int FindClosestCaretIndexInLine(TextLayout layout, int lineStart, int lineEnd, int targetX)
    {
        int bestIndex = lineStart;
        var minDistance = int.MaxValue;

        for (int i = lineStart; i <= lineEnd; i++)
        {
            int x = i == lineStart ? 0 : layout.Glyphs[i - 1].BBOX.Right;
            int dist = Math.Abs(x - targetX);
            if (dist >= minDistance)
            {
                continue;
            }
            
            minDistance = dist;
            bestIndex = i;
        }
        
        return bestIndex;
    }
}