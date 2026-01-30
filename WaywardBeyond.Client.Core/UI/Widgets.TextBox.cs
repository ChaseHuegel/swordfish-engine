using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
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
            ui.LayoutDirection = LayoutDirection.None;
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Left,
            };

            bool clicked = ui.Clicked();
            bool held = ui.Held();
            bool hovering = ui.Hovering();
            bool entered = ui.Entered();
            bool exited = ui.Exited();
            bool focused = ui.Focused();
            var typing = false;
            var navigating = false;

            if (focused)
            {
                bool isCtrlHeld = inputService.IsKeyHeld(Key.Control);
                bool isShiftHeld = inputService.IsKeyHeld(Key.Shift);
                
                IReadOnlyCollection<UIController.Input> inputBuffer = ui.GetInputBuffer();
                foreach (UIController.Input input in inputBuffer)
                {
                    if (input.Type == UIController.InputType.Char)
                    {
                        state.Text.Insert(state.CaretIndex, input.Char);
                        state.CaretIndex += 1;
                        typing = true;
                    }
                    else if (input == UIController.Key.Backspace && state.CaretIndex <= state.Text.Length && state.CaretIndex > 0)
                    {
                        int deleteStartIndex = state.CaretIndex - 1;
                        var countToDelete = 1;
                        
                        if (isCtrlHeld)
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
                        }
                        
                        state.Text.Remove(deleteStartIndex, countToDelete);
                        state.CaretIndex -= countToDelete;
                        typing = true;
                    }
                    else if (input == UIController.Key.Delete && state.CaretIndex <= state.Text.Length - 1)
                    {
                        var countToDelete = 1;
                        if (isCtrlHeld)
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
                        
                        state.Text.Remove(state.CaretIndex, countToDelete);
                        typing = true;
                    }
                    else if (input == UIController.Key.Tab)
                    {
                        state.Text.Append('\t');
                        state.CaretIndex += 1;
                        typing = true;
                    }
                    else if (input == UIController.Key.LeftArrow)
                    {
                        if (isCtrlHeld)
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
                    
                    state.CaretIndex = Math.Clamp(state.CaretIndex, 0, state.Text.Length);
                    if (typing || (navigating && !isShiftHeld))
                    {
                        state.SelectionStartIndex = state.CaretIndex;
                    }
                }
            }
            
            bool isPlaceholder = state.Text.Length == 0;
            string displayString = isPlaceholder ? state.PlaceholderText ?? " " : state.Text.ToString();
            
            TextConstraints caretConstraints = ui.Measure(fontOptions, displayString, 0, state.CaretIndex);
            if (focused)
            {
                //  Render the caret
                using (ui.Element())
                {
                    ui.Color = new Vector4(1f, 1f, 1f, 1f);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Right,
                        X = new Fixed(caretConstraints.MinWidth + 2),
                        Width = new Fixed(2),
                        Height = new Fill(),
                    };
                }
            }
            
            using (ui.Text(displayString))
            {
                ui.LayoutDirection = LayoutDirection.None;
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
            if (state.SelectionStartIndex != state.CaretIndex)
            {
                var flipped = false;
                int selectionStartIndex;
                int selectionLength;
                if (state.SelectionStartIndex < state.CaretIndex)
                {
                    selectionStartIndex = state.SelectionStartIndex;
                    selectionLength = state.CaretIndex - state.SelectionStartIndex;
                }
                else
                {
                    selectionStartIndex = state.CaretIndex;
                    selectionLength = state.SelectionStartIndex - state.CaretIndex;
                    flipped = true;
                }

                TextConstraints selectionConstraints = ui.Measure(fontOptions, displayString, selectionStartIndex, selectionLength);
                
                using (ui.Element())
                {
                    ui.Color = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);
                    ui.Constraints = new Constraints
                    {
                        Anchors = Anchors.Right,
                        X = new Fixed(flipped ? caretConstraints.MinWidth + selectionConstraints.MinWidth : caretConstraints.MinWidth),
                        Width = new Fixed(selectionConstraints.MinWidth),
                        Height = new Fill(),
                    };
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
            
            if (focused && inputService.IsKeyPressed(Key.Enter))
            {
                interactions |= Interactions.Submit;
            }

            return interactions != Interactions.None;
        }
    }
}