using System;
using Swordfish.Audio;
using WaywardBeyond.Client.Core.Configuration;

namespace WaywardBeyond.Client.Core.UI;

internal static partial class Widgets
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
        
        audioService.Play("sounds/menu sounds_exit click 1.wav", volumeSettings.MixInterface());
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
        
        audioService.Play("sounds/misc effects_click 1.wav", volumeSettings.MixInterface());
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
        
        audioService.Play("sounds/misc effects_tap 2.wav", volumeSettings.MixInterface());
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
        
        audioService.Play("sounds/misc effects_tap 1.wav", volumeSettings.MixInterface());
        return true;
    }
    
    /// <summary>
    ///     Plays standard text audio for interactions.
    /// </summary>
    /// <returns>True if submitted; otherwise false.</returns>
    public static bool WithTextInputAudio(this Interactions interactions, IAudioService audioService, VolumeSettings volumeSettings)
    {
        if ((interactions & Interactions.Click) == Interactions.Click)
        {
            audioService.Play("sounds/misc effects_click 1.wav", volumeSettings.MixInterface());
        }
        
        if ((interactions & Interactions.Submit) != Interactions.Submit)
        {
            return false;
        }
        
        audioService.Play("sounds/menu sounds_exit click 1.wav", volumeSettings.MixInterface());
        return true;
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
        Submit = 32,
    }
}