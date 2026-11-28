namespace WaywardBeyond.Gameplay.Power;

/// <summary>Authoritative balance state of a structure-level power bus.</summary>
public enum PowerGridStatus : byte
{
    /// <summary>Enabled demand fits inside enabled supply.</summary>
    Nominal = 0,

    /// <summary>Demand exceeds supply and at least the highest-priority consumer tier keeps power.</summary>
    Brownout = 1,

    /// <summary>Demand exceeds supply and even the highest-priority consumer tier cannot be powered.</summary>
    Overload = 2,
}
