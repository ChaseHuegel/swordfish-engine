namespace Swordfish.Audio;

/// <summary>Per-tick snapshot of global spatial audio tunables.</summary>
internal readonly record struct SpatialAudioConfig(
    float SpeedOfSound,
    float DopplerScale,
    float MinDopplerPitch,
    float MaxDopplerPitch
);
