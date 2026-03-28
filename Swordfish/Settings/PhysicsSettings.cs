using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed class PhysicsSettings : Config<PhysicsSettings>
{
    public DataBinding<bool> AccumulateUpdates { get; private set; } = new(true);
}