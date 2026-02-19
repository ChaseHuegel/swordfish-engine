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
            ui.Padding = new Padding(left: 1, top: 0, right: 1, bottom: 0);
            
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

            TextLayout previousTextLayout = ui.GetTextLayout(id + "_Text");
            
            if (clicked || held)
            {
                IntVector2 relativeCursorPosition = ui.GetRelativeCursorPosition();
                selectionOverwritten = true;
                
                var xOffset = 0;
                for (var i = 0; i < state.Text.Length; i++)
                {
                    TextConstraints charConstraints = ui.TextEngine.Measure(fontOptions, state.Text[i].ToString(), 0, 1);
                    
                    int halfWidth = charConstraints.MinWidth / 2;
                    if (relativeCursorPosition.X > xOffset - halfWidth)
                    {
                        state.CaretIndex = i;
                        
                        if (!held)
                        {
                            state.SelectionStartIndex = i;
                        }
                    }
                    
                    xOffset += charConstraints.MinWidth;

                    //  Allow selecting any character at the end
                    if (relativeCursorPosition.X <= xOffset - halfWidth)
                    {
                        continue;
                    }

                    state.CaretIndex++;
                    if (!held)
                    {
                        state.SelectionStartIndex++;
                    }
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
                                countToDelete = state.CaretIndex + 1;
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
                        if (inputService.IsKeyHeld(Key.Control))
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
            }
            else
            {
                state.SelectionStartIndex = state.CaretIndex;
            }
            
            bool isPlaceholder = state.Text.Length == 0;
            string displayString = isPlaceholder ? state.Settings.Placeholder ?? " " : state.Text.ToString();
            
            TextLayout caretLayout = ui.TextEngine.Layout(fontOptions, state.CaretIndex > 0 ? displayString : "\0", 0, state.CaretIndex > 0 ? state.CaretIndex : 1, previousTextLayout.Constraints.PreferredWidth);
            GlyphLayout caretGlyph = caretLayout.Glyphs.Length > 0 ? caretLayout.Glyphs[^1] : default;
            
            if (focused && (editing || navigating || ui.Time % 1f < 0.5f))
            {
                //  Render the caret
                using (ui.Element())
                {
                    ui.Color = new Vector4(1f, 1f, 1f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Bottom | Anchors.Left | Anchors.Local,
                        X = new Fixed(caretGlyph.BBOX.Right),
                        Y = new Fixed(caretLayout.Constraints.PreferredHeight),
                        Width = new Fixed(2),
                        Height = new Fixed(caretLayout.LineHeight),
                    };
                }
            }
            
            using (ui.Text(displayString))
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

                if (isPlaceholder)
                {
                    ui.Color *= new Vector4(0.5f, 0.5f, 0.5f, 1f);
                }
            }
            
            //  Render text selection
            if (state.HasSelection())
            {
                TextBoxState.Selection selection = state.CalculateSelection();
                TextLayout selectionLayout = ui.TextEngine.Layout(fontOptions, displayString, selection.StartIndex, selection.Length, previousTextLayout.Constraints.PreferredWidth);
                int x = caretLayout.Glyphs[selection.StartIndex].BBOX.Left;

                var glyphOffset = 0;
                for (var i = 0; i < selectionLayout.Lines.Length; i++)
                {
                    int stride = selectionLayout.Lines[i].Length;
                    
                    var width = 0;
                    for (int n = glyphOffset; n < glyphOffset + stride; n++)
                    {
                        width = selectionLayout.Glyphs[n].BBOX.Right;
                    }

                    //  Render the current line
                    using (ui.Element())
                    {
                        ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
                        ui.Constraints = new Constraints
                        {
                            X = new Fixed(x),
                            Y = new Fixed(i * selectionLayout.LineHeight),
                            Width = new Fixed(width),
                            Height = new Fixed(selectionLayout.LineHeight),
                        };
                    }
                    
                    glyphOffset += stride;
                    x = 0;
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
}