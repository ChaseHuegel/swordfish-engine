namespace WaywardBeyond.AssetTools.Settings;

/// <summary>
///     Diagnostic log verbosity level for Asset Studio and the CLI.
/// </summary>
public enum ToolLogLevel
{
    /// <summary>
    ///     Emit verbose trace diagnostics.
    /// </summary>
    Trace = 0,

    /// <summary>
    ///     Emit debug diagnostics.
    /// </summary>
    Debug = 1,

    /// <summary>
    ///     Emit standard operational information.
    /// </summary>
    Information = 2,

    /// <summary>
    ///     Emit warnings and errors only.
    /// </summary>
    Warning = 3,

    /// <summary>
    ///     Emit errors only.
    /// </summary>
    Error = 4
}
