namespace WaywardBeyond.Gameplay.Interactions;

/// <summary>The outcome of running every stage for one interaction.</summary>
public readonly struct InteractionRunResult(in InteractionResolution resolution, InteractionDecision decision)
{
    /// <summary>The running resolution after the pipeline stopped or completed.</summary>
    public readonly InteractionResolution Resolution = resolution;

    /// <summary>
    /// The terminal decision: <see cref="InteractionDecision.Consume"/> or
    /// <see cref="InteractionDecision.Cancel"/> when the pipeline stopped early, otherwise
    /// <see cref="InteractionDecision.Continue"/>.
    /// </summary>
    public readonly InteractionDecision Decision = decision;
}
