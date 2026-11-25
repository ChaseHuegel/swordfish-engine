namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Outcome of game install discovery without guessing when multiple installations are found.
/// </summary>
public readonly record struct InstallLocateResult(IReadOnlyList<InstallCandidate> Candidates)
{
    /// <summary>
    ///     Gets a value indicating whether more than one candidate installation was discovered.
    /// </summary>
    public bool IsAmbiguous => Candidates.Count > 1;

    /// <summary>
    ///     Gets the sole discovered installation, or null when zero or multiple candidates exist.
    /// </summary>
    public InstallCandidate? SingleMatch => Candidates.Count == 1 ? Candidates[0] : null;
}
