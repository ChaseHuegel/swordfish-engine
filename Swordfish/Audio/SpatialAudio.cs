using Krill;
using Swordfish.ECS;

namespace Swordfish.Audio;

/// <summary>Enables 3D spatialization for an audio entity that also has a <see cref="TransformComponent"/>.</summary>
public struct SpatialAudio : IDataComponent
{
    /// <summary>Distance within which the source plays at full volume.</summary>
    public float MinDistance = 4f;

    /// <summary>Distance at which the source attenuates to silence; values at or below <see cref="MinDistance"/> leave inverse and exponential curves unbounded.</summary>
    public float MaxDistance = 64f;

    /// <summary>Controls how quickly volume falls off with distance; 0 disables attenuation.</summary>
    public float RolloffFactor = 1f;

    /// <summary>Distance attenuation curve applied between <see cref="MinDistance"/> and <see cref="MaxDistance"/>.</summary>
    public AudioRolloffMode RolloffMode = AudioRolloffMode.Inverse;

    /// <summary>Intensity multiplier for Doppler pitch shift on this source; 0 disables Doppler.</summary>
    public float DopplerFactor = 1f;

    /// <summary>Blend between 2D playback (0) and full 3D spatialization (1).</summary>
    public float SpatialBlend = 1f;

    /// <summary>Creates spatial audio parameters with default attenuation, panning, and Doppler settings.</summary>
    public SpatialAudio()
    {
    }

    /// <summary>Creates spatial audio parameters with explicit attenuation, rolloff, Doppler, and blend values.</summary>
    public SpatialAudio(
        float minDistance,
        float maxDistance,
        float rolloffFactor = 1f,
        AudioRolloffMode rolloffMode = AudioRolloffMode.Inverse,
        float dopplerFactor = 1f,
        float spatialBlend = 1f
    ) {
        MinDistance = minDistance;
        MaxDistance = maxDistance;
        RolloffFactor = rolloffFactor;
        RolloffMode = rolloffMode;
        DopplerFactor = dopplerFactor;
        SpatialBlend = spatialBlend;
    }
}
