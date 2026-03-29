using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed record BloomSettings
{
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    public DataBinding<int> Passes { get; private set; } = new(10);
}