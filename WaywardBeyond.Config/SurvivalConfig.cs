using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Adventure Mode survival tuning: vital maximums and oxygen exchange rates.</summary>
public sealed record SurvivalConfig
{
    /// <summary>The default maximum health of a new Adventure character.</summary>
    public DataBinding<float> MaxHealth { get; private set; } = new(100f);

    /// <summary>The default maximum oxygen of a new Adventure character.</summary>
    public DataBinding<float> MaxOxygen { get; private set; } = new(100f);

    /// <summary>The oxygen drained per second in vacuum (<c>wb:pressurized == 0</c>).</summary>
    public DataBinding<float> OxygenDrainPerSecond { get; private set; } = new(2f);

    /// <summary>The oxygen restored per second in a pressurized room (<c>wb:pressurized == 1</c>).</summary>
    public DataBinding<float> OxygenRefillPerSecond { get; private set; } = new(20f);

    /// <summary>The damage per second applied while oxygen stays at zero.</summary>
    public DataBinding<float> SuffocationDamagePerSecond { get; private set; } = new(5f);

    /// <summary>The cadence in seconds at which continuous oxygen and buff writes replicate.</summary>
    public DataBinding<float> UpdateIntervalSeconds { get; private set; } = new(0.25f);

    /// <summary>Bundled blueprint ID spawned as a recoverable Death-Container when an Adventure player dies with items.</summary>
    public DataBinding<string> DeathContainerBlueprintId { get; private set; } = new("builtin/death_container");
}
