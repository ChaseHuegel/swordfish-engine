namespace WaywardBeyond.Gameplay.Stations;

/// <summary>Outcome returned by an <see cref="IStationActionHandler"/>.</summary>
public enum StationActionDecision : byte
{
    /// <summary>Decline the op and pass it to the next matching handler.</summary>
    Continue = 0,

    /// <summary>Accept and complete the op; stop dispatch.</summary>
    Handled = 1,

    /// <summary>Reject the op as invalid or unauthorized; stop dispatch.</summary>
    Rejected = 2,
}
