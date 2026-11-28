using Krill;

namespace WaywardBeyond.Gameplay.Components;

/// <summary>
/// Server-side power rating for one brick-entity. Attached by the power brick-entity decorator from
/// the brick's <c>wb:power_kw</c> and <c>wb:power_standby_kw</c> statistics.
/// </summary>
public struct PowerNodeComponent : IDataComponent
{
    /// <summary>Signed nominal rating in kilowatts; positive generates power and negative draws power.</summary>
    public float RatedKw;

    /// <summary>Standby draw in kilowatts while the machine is enabled but idle; zero means draw the full rating.</summary>
    public float StandbyKw;

    /// <summary>Deterministic brownout tier; lower values keep power first.</summary>
    public byte Priority;

    /// <summary>Whether the brick-entity declares a fuel slot group and can burn generator recipes.</summary>
    public bool HasFuelGroup;

    /// <summary>Whether this node generates power.</summary>
    public readonly bool IsProducer => RatedKw > 0f;

    /// <summary>The rated demand in kilowatts, or zero for a producer.</summary>
    public readonly float DemandKw => RatedKw < 0f ? -RatedKw : 0f;
}
