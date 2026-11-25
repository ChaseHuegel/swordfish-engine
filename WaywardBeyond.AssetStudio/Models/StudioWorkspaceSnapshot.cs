namespace WaywardBeyond.AssetStudio.Models;

/// <summary>
///     Bundles the complete workspace payload returned when opening a workspace.
/// </summary>
public sealed class StudioWorkspaceSnapshot
{
    /// <summary>
    ///     Display name of the opened pack or repository.
    /// </summary>
    public required string WorkspaceName { get; init; }

    /// <summary>
    ///     Active workspace mode.
    /// </summary>
    public required WorkspaceMode Mode { get; init; }

    /// <summary>
    ///     Primary writable root path.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    ///     Optional reference game install path when in pack mode.
    /// </summary>
    public string? ReferenceRootPath { get; init; }

    /// <summary>
    ///     Content schema version supported by the studio.
    /// </summary>
    public int ContentVersion { get; init; } = 1;

    /// <summary>
    ///     Pack identifier for staged exports.
    /// </summary>
    public required string PackId { get; init; }

    /// <summary>
    ///     Default namespace prefix for newly authored content.
    /// </summary>
    public required string PackNamespace { get; init; }

    /// <summary>
    ///     Effective per-workspace default namespace prefix for newly authored assets.
    /// </summary>
    public string DefaultNamespace { get; init; } = string.Empty;

    /// <summary>
    ///     Shader stem names discovered across workspace shader directories.
    /// </summary>
    public IReadOnlyList<string> AvailableShaders { get; init; } = [];

    /// <summary>
    ///     Image files discovered recursively across enabled workspace roots.
    /// </summary>
    public IReadOnlyList<WorkspaceImageEntry> WorkspaceImages { get; init; } = [];

    /// <summary>
    ///     Whether the opened pack directory lacks pack metadata, a module manifest, and an <c>assets/</c> folder.
    /// </summary>
    public bool IsUninitializedFolder { get; init; }

    /// <summary>
    ///     All loaded asset document seeds.
    /// </summary>
    public required IReadOnlyList<StudioAssetSeed> Assets { get; init; }

    /// <summary>
    ///     Initial validation findings across workspace roots.
    /// </summary>
    public required IReadOnlyList<ValidationFinding> Findings { get; init; }

    /// <summary>
    ///     Searchable reference picker candidates indexed across workspace roots.
    /// </summary>
    public required IReadOnlyList<ReferenceCandidate> Candidates { get; init; }

    /// <summary>
    ///     Unclean-exit draft files available for startup recovery.
    /// </summary>
    public required IReadOnlyList<DraftRecoveryEntry> PendingDrafts { get; init; }
}
