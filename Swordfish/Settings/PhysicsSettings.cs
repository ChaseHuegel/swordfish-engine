using System.Numerics;
using Swordfish.Library.Configuration;
using Swordfish.Library.Types;

namespace Swordfish.Settings;

public sealed class PhysicsSettings : Config<PhysicsSettings>
{
    /// <summary>Whether physics steps accumulate to catch up a lagging world.</summary>
    public DataBinding<bool> AccumulateUpdates { get; private set; } = new(true);

    /// <summary>The gravity vector of the default physics system.</summary>
    public DataBinding<Vector3> Gravity { get; private set; } = new(new Vector3(0f, -9.81f, 0f));
}