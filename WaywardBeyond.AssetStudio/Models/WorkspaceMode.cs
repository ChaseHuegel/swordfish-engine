namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Defines whether the studio edits a single pack or a full checkout repository.
/// </summary>
public enum WorkspaceMode
{
    /// <summary>
    ///     Single writable pack root layered over a reference game install.
    /// </summary>
    Pack,

    /// <summary>
    ///     Writable module roots across a source checkout.
    /// </summary>
    Repo,
}
