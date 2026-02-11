using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Audio;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
{
    /// <summary>
    ///     Creates a text-only button, with audio cues.
    /// </summary>
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, ButtonOptions options)
    {
        using (ui.TextButton(id, text, options, out Interactions interactions))
        {
            return interactions.Has(Interactions.Click);
        }
    }
    
    /// <summary>
    ///     Creates a text-only button.
    /// </summary>
    public static bool TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions)
    {
        using (ui.TextButton(id, text, fontOptions, out Interactions interactions))
        {
            return interactions.Has(Interactions.Click);
        }
    }
    
    /// <summary>
    ///     Creates a text-only button, with audio cues.
    /// </summary>
    public static UIBuilder<T>.Scope TextButton<T>(this UIBuilder<T> ui, string id, string text, ButtonOptions options, out Interactions interactions)
    {
        UIBuilder<T>.Scope scope = TextButton(ui, id, text, options.FontOptions, out interactions); 
        interactions.WithButtonAudio(options.AudioOptions.AudioService, options.AudioOptions.VolumeSettings);
        return scope;
    }
    
    /// <summary>
    ///     Creates a text-only button.
    /// </summary>
    public static UIBuilder<T>.Scope TextButton<T>(this UIBuilder<T> ui, string id, string text, FontOptions fontOptions, out Interactions interactions)
    {
        UIBuilder<T>.Scope scope = ui.Element(id);
        
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

        return scope;
    }

    public readonly record struct AudioOptions(IAudioService AudioService, VolumeSettings VolumeSettings);

    public readonly record struct ButtonOptions(FontOptions FontOptions, AudioOptions AudioOptions);
}