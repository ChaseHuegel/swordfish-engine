using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed record SAOSettings
{
    public DataBinding<bool> Enabled { get; private set; } = new(true);
    public DataBinding<int> Samples { get; private set; } = new(8);
}