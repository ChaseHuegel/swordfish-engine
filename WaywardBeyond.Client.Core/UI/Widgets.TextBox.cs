using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Reef;
using Reef.Constraints;
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
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, ref string text, FontOptions fontOptions, IInputService inputService)
    {
        return TextBox(ui, id, ref text, fontOptions, inputService, out Interactions interactions) && (interactions & Interactions.Submit) == Interactions.Submit;
    }
    
    /// <summary>
    ///     Creates a text box that can be typed into, with audio cues.
    /// </summary>
    /// <returns>True if the text box was submitted; otherwise false.</returns>
    public static bool TextBox<T>(this UIBuilder<T> ui, string id, ref string text, FontOptions fontOptions, IInputService inputService, IAudioService audioService, VolumeSettings volumeSettings)
    {
        return TextBox(ui, id, ref text, fontOptions, inputService, out Interactions interactions) && interactions.WithTextInputAudio(audioService, volumeSettings);
    }

    /// <summary>
    ///     Creates a text box that can be typed into.
    /// </summary>
    /// <returns>True if the text box is being interacted with; otherwise false.</returns>
    public static bool TextBox<T>(this UIBuilder<T> ui, string id, ref string text, FontOptions fontOptions, IInputService inputService, out Interactions interactions)
    {
        using (ui.Element(id))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
                Width = new Fixed(100),
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
                var modifiedText = new StringBuilder(text);

                bool shiftHeld = inputService.IsKeyHeld(Key.Shift);

                IReadOnlyCollection<UIController.Input> inputBuffer = ui.GetInputBuffer();
                foreach (UIController.Input input in inputBuffer)
                {
                    if (input == UIController.Input.Backspace)
                    {
                        modifiedText.Remove(modifiedText.Length - 1, 1);
                        typing = true;
                    }

                    if (input >= UIController.Input.D0 && input <= UIController.Input.Z)
                    {
                        char c = shiftHeld ? char.ToUpper((char)input) : char.ToLower((char)input);
                        modifiedText.Append(c);
                        typing = true;
                    }
                }
                
                text = modifiedText.ToString();
            }

            using (ui.Text(text))
            {
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