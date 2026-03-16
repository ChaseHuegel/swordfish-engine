using System;
using Swordfish.Audio;
using WaywardBeyond.Client.Core.Services;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
{
    private const string InterfaceAudioChannel = "interface";
    
    /// <summary>
    ///     Plays standard button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonAudio(this Interactions interactions, SoundEffectService soundEffectService)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        soundEffectService.Play(id: "sounds/menu sounds_exit click 1.wav", InterfaceAudioChannel);
        return true;
    }
    
    /// <summary>
    ///     Plays toggle button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonToggleAudio(this Interactions interactions, SoundEffectService soundEffectService)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        soundEffectService.Play(id: "sounds/misc effects_click 1.wav", InterfaceAudioChannel);
        return true;
    }
    
    /// <summary>
    ///     Plays increase button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonIncreaseAudio(this Interactions interactions, SoundEffectService soundEffectService)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        soundEffectService.Play(id: "sounds/misc effects_tap 2.wav", InterfaceAudioChannel);
        return true;
    }
    
    /// <summary>
    ///     Plays decrease button audio for interactions.
    /// </summary>
    /// <returns>True if clicked; otherwise false.</returns>
    public static bool WithButtonDecreaseAudio(this Interactions interactions, SoundEffectService soundEffectService)
    {
        if ((interactions & Interactions.Click) != Interactions.Click)
        {
            return false;
        }
        
        soundEffectService.Play(id: "sounds/misc effects_tap 1.wav", InterfaceAudioChannel);
        return true;
    }
    
    /// <summary>
    ///     Plays standard text audio for interactions.
    /// </summary>
    /// <returns>True if submitted; otherwise false.</returns>
    public static bool WithTextInputAudio(this Interactions interactions, SoundEffectService soundEffectService)
    {
        if ((interactions & Interactions.Input) == Interactions.Input)
        {
            soundEffectService.Play(id: "sounds/misc effects_click 1.wav", InterfaceAudioChannel);
        }
        
        if ((interactions & Interactions.Submit) != Interactions.Submit)
        {
            return false;
        }
        
        soundEffectService.Play(id: "sounds/menu sounds_exit click 1.wav", InterfaceAudioChannel);
        return true;
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