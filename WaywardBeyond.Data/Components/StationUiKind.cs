namespace WaywardBeyond.Data.Components;

/// <summary>Identifies which client interface an <see cref="ActiveStationComponent"/> opens.</summary>
public enum StationUiKind : byte
{
    /// <summary>No station interface is open.</summary>
    None = 0,

    /// <summary>Opens the contextual station tab inside the player's split overlay.</summary>
    ContextualTab = 1,

    /// <summary>Opens the standalone WAY-OS terminal interface.</summary>
    Terminal = 2,
}
