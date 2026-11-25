namespace WaywardBeyond.AssetTools.Workspace;

/// <summary>
///     Identifies whether the opened workspace edits repository modules or a standalone content pack.
/// </summary>
public enum WorkspaceMode
{
    /// <summary>
    ///     Module directories inside a repository checkout with all enabled roots writable.
    /// </summary>
    Repo = 0,

    /// <summary>
    ///     One writable content pack directory layered over optional read-only game install reference roots.
    /// </summary>
    Pack = 1
}
