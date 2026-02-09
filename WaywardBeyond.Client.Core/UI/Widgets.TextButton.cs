using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Audio;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
{
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
                Anchors = Anchors.Center | Anchors.Top,
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
}