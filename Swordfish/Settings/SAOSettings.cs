using Swordfish.Library.Types;

namespace Swordfish.Settings;

/// <summary>Settings controlling Scalable Ambient Obscurance.</summary>
public sealed record SAOSettings
{
    /// <summary>Enables the SAO pass and the composite.</summary>
    public DataBinding<bool> Enabled { get; private set; } = new(true);

    /// <summary>Number of obscurance samples per AO pixel. Cost is linear in this value.</summary>
    public DataBinding<int> Samples { get; private set; } = new(8);

    /// <summary>AO buffer size as a fraction of the screen.</summary>
    public DataBinding<float> ResolutionScale { get; private set; } = new(0.5f);

    /// <summary>World-space obscurance radius in meters.</summary>
    public DataBinding<float> Radius { get; private set; } = new(0.35f);

    /// <summary>Occlusion strength multiplier.</summary>
    public DataBinding<float> Intensity { get; private set; } = new(1f);

    /// <summary>Constant normal bias in meters. Suppresses self-occlusion.</summary>
    public DataBinding<float> Bias { get; private set; } = new(0.02f);

    /// <summary>Exponent of the visibility curve. 1.5 matches the HPG12 reference.</summary>
    public DataBinding<float> Power { get; private set; } = new(1.5f);

    /// <summary>View distance in meters where the AO clip starts.</summary>
    public DataBinding<float> MaxDistance { get; private set; } = new(48f);

    /// <summary>Width in meters of the fade at the distance clip. Zero is a hard cut.</summary>
    public DataBinding<float> FadeRange { get; private set; } = new(8f);

    /// <summary>Number of separable blur passes. Each pass costs 9 AO taps.</summary>
    public DataBinding<int> BlurPasses { get; private set; } = new(2);
}
