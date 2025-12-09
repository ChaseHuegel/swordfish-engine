using System;
using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal static class Widgets
{
    /// <summary>
    ///     Plays standard button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonAudio(this Interactions interactions, IAudioService audioService, VolumeSettings volumeSettings)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        audioService.Play("sounds/menu sounds_exit click 1.wav", volumeSettings.Master);
        return true;
    }
    
    /// <summary>
    ///     Plays toggle button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonToggleAudio(this Interactions interactions, IAudioService audioService, VolumeSettings volumeSettings)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        audioService.Play("sounds/misc effects_click 1.wav", volumeSettings.Master);
        return true;
    }
    
    /// <summary>
    ///     Plays increase button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonIncreaseAudio(this Interactions interactions, IAudioService audioService, VolumeSettings volumeSettings)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        audioService.Play("sounds/misc effects_tap 2.wav", volumeSettings.Master);
        return true;
    }
    
    /// <summary>
    ///     Plays decrease button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonDecreaseAudio(this Interactions interactions, IAudioService audioService, VolumeSettings volumeSettings)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        audioService.Play("sounds/misc effects_tap 1.wav", volumeSettings.Master);
        return true;
    }
    
    /// <summary>
    ///     Creates a text-only button.
    /// </summary>
    /// <returns>True if the button was clicked; otherwise false.</returns>
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions)
    {
        return TextButton(ui, id, text, fontOptions, out Interactions interactions) && (interactions & Interactions.Click) == Interactions.Click;
    }
    
    /// <summary>
    ///     Creates a text-only button, with audio cues.
    /// </summary>
    /// <returns>True if the button was clicked; otherwise false.</returns>
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions, IAudioService audioService, VolumeSettings volumeSettings)
    {
        return TextButton(ui, id, text, fontOptions, out Interactions interactions) && interactions.WithButtonAudio(audioService, volumeSettings);
    }
    
    /// <summary>
    ///     Creates a text-only button.
    /// </summary>
    /// <returns>True if the button is being interacted with; otherwise false.</returns>
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions, out Interactions interactions)
    {
        using (ui.Element(id))
        {
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center,
                X = new Relative(0.5f),
            };
            
            bool clicked = ui.Clicked();
            bool held = ui.Held();
            bool hovering = ui.Hovering();
            bool entered = ui.Entered();
            bool exited = ui.Exited();
            
            using (ui.Text(text))
            {
                ui.FontOptions = fontOptions;

                if (clicked)
                {
                    ui.Color = new Vector4(0f, 0f, 0f, 1f);
                }
                else if (hovering)
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
            
            return interactions != Interactions.None;
        }
    }
    
    /// <summary>
    ///     Creates a text-only checkbox.
    /// </summary>
    /// <returns>True if the box is currently checked; otherwise false.</returns>
    public static bool Checkbox<T>(this UIBuilder<T> ui, string id, string text, bool isChecked)
    {
        return Checkbox<T>(ui, id, text, isChecked, out _);
    }
    
    /// <summary>
    ///     Creates a text-only checkbox, with audio cues.
    /// </summary>
    /// <returns>True if the box is currently checked; otherwise false.</returns>
    public static bool Checkbox<T>(this UIBuilder<T> ui, string id, string text, bool isChecked, IAudioService audioService, VolumeSettings volumeSettings)
    {
        bool value = Checkbox(ui, id, text, isChecked, out Interactions interactions);
        interactions.WithButtonToggleAudio(audioService, volumeSettings);
        return value;
    }
    
    /// <summary>
    ///     Creates a text-only checkbox.
    /// </summary>
    /// <returns>True if the box is currently checked; otherwise false.</returns>
    public static bool Checkbox<T>(this UIBuilder<T> ui, string id, string text, bool isChecked, out Interactions interactions)
    {
        const string checkedUnicode = "\uf14a";
        const string uncheckedUnicode = "\uf0c8";
        
        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Constraints = new Constraints
            {
                Width = new Fill(),
            };
            
            using (ui.Text(text))
            {
                ui.FontSize = 20;
                ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
                ui.Constraints = new Constraints
                {
                    Anchors = Anchors.Center | Anchors.Left,
                    Y = new Relative(0.5f),
                };
            }

            using (ui.Element())
            {
                ui.Constraints = new Constraints
                {
                    Width = new Fill(),
                    Height = new Fill(),
                };
            }

            using (ui.Element(id))
            {
                bool clicked = ui.Clicked();
                bool held = ui.Held();
                bool hovering = ui.Hovering();
                bool entered = ui.Entered();
                bool exited = ui.Exited();
                
                using (ui.Text(isChecked ? checkedUnicode : uncheckedUnicode, fontID: "Font Awesome 6 Free Regular"))
                {
                    ui.FontSize = 30;
                    
                    //  TODO swordfish#233 For some reason some FA glyphs are rendering outside of their bounds
                    ui.Padding = new Padding
                    {
                        Right = 4,
                        Bottom = 12,
                    };
                    
                    if (clicked)
                    {
                        ui.Color = new Vector4(0f, 0f, 0f, 1f);
                        isChecked = !isChecked;
                    }
                    else if (hovering)
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
            }
        }

        return isChecked;
    }

    [Flags]
    public enum Interactions
    {
        None = 0,
        Click = 1,
        Held = 2,
        Hover = 4,
        Enter = 8,
        Exit = 16,
    }
}