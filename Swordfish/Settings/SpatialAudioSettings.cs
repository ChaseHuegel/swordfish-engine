using Swordfish.Library.Types;

namespace Swordfish.Settings;

/// <summary>Runtime settings for 3D spatial audio attenuation and Doppler pitch shift.</summary>
public sealed record SpatialAudioSettings
{
    /// <summary>Speed of sound in world units per second used for Doppler pitch shift.</summary>
    public DataBinding<float> SpeedOfSound { get; private set; } = new(343f);

    /// <summary>Global multiplier applied to Doppler pitch shift; 0 disables Doppler globally.</summary>
    public DataBinding<float> DopplerScale { get; private set; } = new(1f);

    /// <summary>Lower bound on the Doppler pitch multiplier.</summary>
    public DataBinding<float> MinDopplerPitch { get; private set; } = new(0.5f);

    /// <summary>Upper bound on the Doppler pitch multiplier.</summary>
    public DataBinding<float> MaxDopplerPitch { get; private set; } = new(2f);
}
