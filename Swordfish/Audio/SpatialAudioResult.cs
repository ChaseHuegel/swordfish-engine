namespace Swordfish.Audio;

/// <summary>Computed volume attenuation, stereo pan, and Doppler pitch multiplier for a spatial source.</summary>
internal readonly record struct SpatialAudioResult(float VolumeMultiplier, float Pan, float PitchMultiplier);
