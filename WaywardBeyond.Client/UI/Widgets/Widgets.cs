using System;
using Reef.UI;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI;

internal static partial class Widgets
{
    /// <summary>Gets the vertical correction for Font Awesome glyphs, which sit low in their line box.</summary>
    private static int IconVerticalOffset(in FontOptions font)
    {
        if (font.ID != UiTheme.FONT_ICONS && font.ID != UiTheme.FONT_BRANDS)
        {
            return 0;
        }

        return (int)MathF.Round(font.Size * 0.125f);
    }

    private const string INTERFACE_AUDIO_CHANNEL = "interface";
    private const string EXIT_CLICK_SOUND = "sounds/menu sounds_exit click 1.wav";
    private const string CLICK_SOUND = "sounds/misc effects_click 1.wav";
    private const string TAP_INCREASE_SOUND = "sounds/misc effects_tap 2.wav";
    private const string TAP_DECREASE_SOUND = "sounds/misc effects_tap 1.wav";

    /// <summary>Plays the interface sound for one menu interaction.</summary>
    public static void PlayInterfaceSound(this SoundEffectService soundEffectService, InterfaceSound sound)
    {
        string id = sound switch
        {
            InterfaceSound.Toggle or InterfaceSound.Input => CLICK_SOUND,
            InterfaceSound.Increase => TAP_INCREASE_SOUND,
            InterfaceSound.Decrease => TAP_DECREASE_SOUND,
            _ => EXIT_CLICK_SOUND,
        };

        soundEffectService.Play(id, INTERFACE_AUDIO_CHANNEL);
    }

    /// <summary>Plays the interface sound when the interactions include a click, and reports the click.</summary>
    public static bool WithButtonAudio(
        this Interactions interactions,
        SoundEffectService soundEffectService,
        InterfaceSound sound = InterfaceSound.Click
    )
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }

        soundEffectService.PlayInterfaceSound(sound);
        return true;
    }

    /// <summary>Plays the text input sounds, and reports whether the interactions include a submit.</summary>
    public static bool WithTextInputAudio(this Interactions interactions, SoundEffectService soundEffectService)
    {
        if ((interactions & Interactions.Input) == Interactions.Input)
        {
            soundEffectService.PlayInterfaceSound(InterfaceSound.Input);
        }

        if ((interactions & Interactions.Submit) != Interactions.Submit)
        {
            return false;
        }

        soundEffectService.PlayInterfaceSound(InterfaceSound.Submit);
        return true;
    }

    /// <summary>Sound effects played by menu interactions.</summary>
    public enum InterfaceSound
    {
        Click,
        Toggle,
        Increase,
        Decrease,
        Input,
        Submit,
    }

    /// <summary>
    ///     Checks if interactions has the specified value.
    /// </summary>
    public static bool Has(this Interactions interactions, Interactions value)
    {
        return (interactions & value) == value;
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
        Input = 32,
        Submit = 64,
    }
}
