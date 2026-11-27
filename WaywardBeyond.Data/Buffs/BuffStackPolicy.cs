namespace WaywardBeyond.Data.Buffs;

/// <summary>Determines how reapplying an active buff affects its expiry tick.</summary>
public enum BuffStackPolicy
{
    /// <summary>Reset expiry to the full duration from the current sim tick.</summary>
    Refresh = 0,

    /// <summary>Add the buff duration onto the existing remaining expiry tick.</summary>
    Extend = 1,

    /// <summary>Leave an already active buff unmodified.</summary>
    Ignore = 2,
}
