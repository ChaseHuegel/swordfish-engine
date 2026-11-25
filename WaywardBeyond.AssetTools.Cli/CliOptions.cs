namespace WaywardBeyond.AssetTools.Cli;

/// <summary>
///     Parsed command-line invocation parameters for the Asset Tools CLI.
/// </summary>
public readonly record struct CliOptions(
    string Command,
    string? SubCommand,
    string? TargetPath,
    bool Json,
    bool CheckOnly,
    string? InstallPath,
    string? PackId,
    string? PackNamespace,
    string? PackName,
    string? PackAuthor);
