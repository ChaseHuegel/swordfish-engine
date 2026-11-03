using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed record BloomSettings
{
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    public DataBinding<int> Passes { get; private set; } = new(2);
    public DataBinding<float> Intensity { get; private set; } = new(1f);
    public DataBinding<float> Threshold { get; private set; } = new();
    public DataBinding<float> Softness { get; private set; } = new();
}
