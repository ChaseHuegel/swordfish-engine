using Swordfish.Library.Types;

namespace Swordfish.Settings;

/// <summary>Settings controlling dynamic shadow rendering.</summary>
public sealed record ShadowSettings
{
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    public DataBinding<ShadowFilterMode> FilterMode { get; private set; } = new(ShadowFilterMode.Soft);
    public DataBinding<int> Resolution { get; private set; } = new(1024);
    public DataBinding<float> MaxDistance { get; private set; } = new(96f);
    public DataBinding<int> CascadeCount { get; private set; } = new(3);
    public DataBinding<int> MaxPointShadows { get; private set; } = new(2);
    public DataBinding<float> NormalBias { get; private set; } = new(0.02f);
    public DataBinding<float> DepthBias { get; private set; } = new(0.005f);
}

