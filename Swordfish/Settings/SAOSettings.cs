using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed record SAOSettings
{
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    public DataBinding<int> Samples { get; private set; } = new(8);
    public DataBinding<float> ResolutionScale { get; private set; } = new(0.5f);
    public DataBinding<float> MaxDistance { get; private set; } = new(48f);
}