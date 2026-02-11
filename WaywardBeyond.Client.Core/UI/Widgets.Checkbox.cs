using System.Numerics;
using Reef;
using Reef.Constraints;
using Reef.UI;
using Swordfish.Audio;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
{
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
            ui.Constraints = new Constraints
            {
                Width = new Relative(1f),
            };
            
            using (ui.Text(text))
            {
                ui.FontSize = 20;
                ui.Color = new Vector4(0.65f, 0.65f, 0.65f, 1f);
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
                    ui.FontSize = 20;
            
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
}