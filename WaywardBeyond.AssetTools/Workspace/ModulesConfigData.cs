namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Read-only data projection of a game <c>config/modules.toml</c> load-order file.
/// </summary>
public readonly record struct ModulesConfigData(
    bool AllowScriptCompilation,
    IReadOnlyList<string> LoadOrder,
    string FilePath);
