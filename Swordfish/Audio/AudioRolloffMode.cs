namespace Swordfish.Audio;

/// <summary>Distance attenuation curve used for spatialized audio sources.</summary>
public enum AudioRolloffMode
{
    /// <summary>Inverse-distance attenuation normalized to reach zero at the maximum distance.</summary>
    Inverse,

    /// <summary>Linear attenuation from full volume at the minimum distance to zero at the maximum distance.</summary>
    Linear,

    /// <summary>Exponential-distance attenuation normalized to reach zero at the maximum distance.</summary>
    Exponential,
}
