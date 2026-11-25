namespace WaywardBeyond.AssetTools.Cli;

/// <summary>
///     Standard exit codes returned by the Asset Tools CLI.
/// </summary>
public static class CliExitCode
{
    /// <summary>
    ///     Command completed cleanly with no blocking findings or mismatches.
    /// </summary>
    public const int CLEAN = 0;

    /// <summary>
    ///     Validation reported broken/fatal findings or round-trip check detected a mismatch.
    /// </summary>
    public const int FINDINGS_OR_MISMATCH = 1;

    /// <summary>
    ///     Command arguments were invalid or an IO error prevented execution.
    /// </summary>
    public const int USAGE_OR_IO_ERROR = 2;
}
