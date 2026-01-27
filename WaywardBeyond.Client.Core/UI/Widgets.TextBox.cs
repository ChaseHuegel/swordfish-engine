using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Reef;
using Reef.Constraints;
using Reef.Text;
using Reef.UI;
using Swordfish.Audio;
using Swordfish.Library.IO;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
{
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

            if (focused)
            {
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
                        state.Text.Remove(state.CaretIndex - 1, 1);
                        state.CaretIndex -= 1;
                        typing = true;
                    }
                    else if (input == UIController.Key.Delete && state.CaretIndex <= state.Text.Length - 1)
                    {
                        state.Text.Remove(state.CaretIndex, 1);
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
                        state.CaretIndex -= 1;
                    }
                    else if (input == UIController.Key.RightArrow)
                    {
                        state.CaretIndex += 1;
                    }
                    else if (input == UIController.Key.Home)
                    {
                        state.CaretIndex = 0;
                    }
                    else if (input == UIController.Key.End)
                    {
                        state.CaretIndex = state.Text.Length;
                    }
                }
            }
            
            state.CaretIndex = Math.Clamp(state.CaretIndex, 0, state.Text.Length);
            
            bool isPlaceholder = state.Text.Length == 0;
            string displayString = isPlaceholder ? state.PlaceholderText ?? " " : state.Text.ToString();
            
            if (focused)
            {
                TextConstraints caretConstraints = ui.Measure(fontOptions, displayString, 0, state.CaretIndex);
                
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