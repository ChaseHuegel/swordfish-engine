namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>Continuous input hold condition required to sustain an active timed interaction.</summary>
public enum TimedInteractionInput : byte
{
    /// <summary>No held input button is required while the timed interaction progresses.</summary>
    None = 0,

    /// <summary>Requires the primary input action to remain held.</summary>
    Primary = 1,

    /// <summary>Requires the secondary input action to remain held.</summary>
    Secondary = 2,
}
