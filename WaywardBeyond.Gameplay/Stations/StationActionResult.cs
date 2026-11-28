namespace WaywardBeyond.Gameplay.Stations;

/// <summary>The decision and optional rejection reason returned by an <see cref="IStationActionHandler"/>.</summary>
public readonly record struct StationActionResult(StationActionDecision Decision, string? Reason = null)
{
    /// <summary>Decline the op so later handlers can process it.</summary>
    public static readonly StationActionResult Continue = new(StationActionDecision.Continue);

    /// <summary>Accept and complete the op.</summary>
    public static readonly StationActionResult Handled = new(StationActionDecision.Handled);

    /// <summary>Reject the op with an optional diagnostic reason.</summary>
    public static StationActionResult Reject(string? reason = null)
    {
        return new StationActionResult(StationActionDecision.Rejected, reason);
    }
}
