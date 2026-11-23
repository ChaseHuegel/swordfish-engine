using System;
using WaywardBeyond.Client.Services;

namespace WaywardBeyond.Client.UI;

/// <summary>Sound and pointer interaction helpers shared by game UI.</summary>
internal static class WidgetInteraction
{
    /// <summary>Plays the interface sound for one menu interaction.</summary>
    public static void PlayInterfaceSound(this SoundEffectService soundEffectService, InterfaceSound sound)
    {
        soundEffectService.PlayInterface(GetSoundTag(sound));
    }

    /// <summary>Gets the invariant sound tag for one menu interaction.</summary>
    internal static string GetSoundTag(InterfaceSound sound) => sound switch
    {
        InterfaceSound.Toggle or InterfaceSound.Input => "ui_click",
        InterfaceSound.Increase => "ui_tap_increase",
        InterfaceSound.Decrease => "ui_tap_decrease",
        _ => "ui_exit",
    };

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
