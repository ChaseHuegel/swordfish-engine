using System.Numerics;
using Krill;

namespace WaywardBeyond.Data.Components;

/// <summary>Replicated active mining laser beam state for a player entity.</summary>
public partial struct LaserBeamComponent : IDataComponent
{
    /// <summary>World-space beam endpoint.</summary>
    public readonly Vector3 Target => new(TargetX, TargetY, TargetZ);

    /// <summary>World-space surface normal at the impact point, or zero on miss.</summary>
    public readonly Vector3 Normal => new(NormalX, NormalY, NormalZ);
}
